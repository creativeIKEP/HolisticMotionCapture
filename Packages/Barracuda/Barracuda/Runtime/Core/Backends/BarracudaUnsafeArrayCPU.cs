using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using Unity.Collections; // Allocator
using Unity.Collections.LowLevel.Unsafe; // UnsafeUtility.Malloc
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Security;


namespace Unity.Barracuda {

/// <summary>
/// `Tensor` data storage based on unsafe array
/// </summary>
public class UnsafeArrayTensorData : SharedArrayTensorData
{
    readonly internal bool m_Readonly = false;

    /// <summary>
    /// Create `UnsafeArrayTensorData` with new array
    /// </summary>
    /// <param name="count">element count to reserve</param>
    public UnsafeArrayTensorData(int count, DataType dataType) : base(new BarracudaArray(count, dataType))
    {
    }

    /// <summary>
    /// Create `UnsafeArrayTensorData` with new array
    /// </summary>
    /// <param name="shape">shape</param>
    public UnsafeArrayTensorData(TensorShape shape, DataType dataType) : this(shape.length, dataType)
    {
    }

    /// <summary>
    /// Create `UnsafeArrayTensorData` and use shared array
    /// </summary>
    /// <param name="sharedArray">shared array</param>
    public UnsafeArrayTensorData(ArrayTensorData sharedArray) : base(sharedArray.array)
    {
    }

    /// <summary>
    /// Create `UnsafeArrayTensorData` and use shared array
    /// </summary>
    /// <param name="sharedArray">shared array</param>
    public UnsafeArrayTensorData(SharedArrayTensorData sharedArray) : base(sharedArray.array, sharedArray.offset, sharedArray.count)
    {
        m_Readonly = true;
    }

    /// <summary>
    /// Create `UnsafeArrayTensorData` from supplied array
    /// </summary>
    /// <param name="data">data</param>
    /// <param name="offset">offset in `data`</param>
    /// <param name="count">element count</param>
    /// <param name="isReadonly">read-only flag</param>
    protected UnsafeArrayTensorData(BarracudaArray data, int offset = 0, int count = -1, bool isReadonly = false) : base(data, offset, count)
    {
        m_Readonly = isReadonly;
    }

    /// <summary>
    /// Finalizer
    /// </summary>
    ~UnsafeArrayTensorData()
    {
        Dispose();
    }

    /// <summary>
    /// Dispose
    /// </summary>
    public override void Dispose()
    {
        m_Array = null;
        m_Offset = m_Count = 0;
    }

    /// <inheritdoc/>
    public override void Reserve(int count)
    {
        if (m_Readonly)
        {
            base.Reserve(count);
            return;
        }

        if (count > maxCapacity)
        {
            m_Array = new BarracudaArray(count, m_Array.Type);
            m_Offset = 0;
            m_Count = m_Array.Length;
        }
    }

    /// <inheritdoc/>
    public override void Upload(float[] data, TensorShape shape, int managedBufferStartIndex = 0)
    {
        if (m_Readonly)
        {
            base.Upload(data, shape, managedBufferStartIndex);
            return;
        }

        var numItemToCopy = shape.length;
        var numItemAvailableInData = data.Length - managedBufferStartIndex;
        Assert.IsTrue(managedBufferStartIndex >= 0);
        Assert.IsTrue(numItemToCopy <= numItemAvailableInData);

        Reserve(numItemToCopy);
        BarracudaArray.Copy(data, managedBufferStartIndex, m_Array, m_Offset, numItemToCopy);
    }

    /// <summary>
    /// Summary
    /// </summary>
    /// <returns>summary</returns>
    public override string ToString()
    {
        return string.Format("(CPU unsafe: {0} length: {1} offset: {2} uploaded: {3})",
            GetHashCode(), m_Array.Length, m_Offset, m_Count);
    }
}

/// <summary>
/// Unsafe array based `IOps` implementation
/// </summary>
public class UnsafeArrayCPUOps : ReferenceCPUOps
{
    internal BLASPlugin blas => m_Blas;
    internal InnerLoop m_InnerLoop = new InnerLoop();

    BLASPlugin m_Blas;

    /// <summary>
    /// Create `UnsafeArrayCPUOps`
    /// </summary>
    /// <param name="allocator">allocator</param>
    public UnsafeArrayCPUOps(ITensorAllocator allocator = null)
    : base(allocator)
    {
        m_Blas = BLASPluginFactory.CreateBLASPlugin();
    }

    /// <summary>
    /// Pin specified `Tensor` to unsafe array based CPU device, if `uploadCache` is false, data is not uploaded to device
    /// </summary>
    /// <param name="X">`Tensor`</param>
    /// <param name="uploadCache">`bool`</param>
    /// <returns>`UnsafeArrayTensorData`</returns>
    public static UnsafeArrayTensorData Pin(Tensor X, bool uploadCache = true)
    {
        X.FlushCache(uploadCache);

        // @TODO: consider abstracting job specific behavior and moving into ITensorData interface
        var asBurstArray = X.tensorOnDevice as BurstTensorData;
        if (asBurstArray != null)
        {
            asBurstArray.fence.Complete();
            asBurstArray.reuse.Complete();
        }

        var onDevice = X.tensorOnDevice as UnsafeArrayTensorData;
        if (onDevice == null)
        {
            // try to adopt CPU arrays
            var asSharedArray = X.tensorOnDevice as SharedArrayTensorData;
            var asArray = X.tensorOnDevice as ArrayTensorData;
            if (asSharedArray != null) X.AttachToDevice(new UnsafeArrayTensorData(asSharedArray));
            else if (asArray != null) X.AttachToDevice(new UnsafeArrayTensorData(asArray));
            else
            {
                if (uploadCache)
                    X.UploadToDevice(new UnsafeArrayTensorData(X.shape, X.dataType)); // device is not compatible, create new array and upload
                else
                    X.AllocateOnDevice(new UnsafeArrayTensorData(X.shape, X.dataType)); // device is not compatible, create new array and upload
            }
        }

        return X.tensorOnDevice as UnsafeArrayTensorData;
    }

    // ---------------------------------------------------------------------------------

    // NOTE: Parallel.For with small number of work items results in varying and often worse performance
    // As a workaround we will fallback to 'for' loop when number of work items is below heuristically determined threshold
    internal static void Parallel_For(long begin, long end, Action<long> body)
    {
        if (end - begin > 2048) // threshold determined heuristically. If work items < threshold, then for loop is faster than Parallel.For()
            Parallel.For(begin, end, body);
        else
            for(var n = begin; n < end; n++)
                body(n);
    }

    /// <inheritdoc/>
    public override Tensor Neg(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Neg(X);

        // f(x) = -x
        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                NegInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    oPtr[i] = -xPtr[i];
                }
            }
        }

        return O;
    }

    /// <inheritdoc/>
    private unsafe void NegInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_negInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Relu(Tensor X)
    {
        if (X.dataType != DataType.Float)
            return base.Relu(X);

        // f(x) = max(x,0.0)
        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 64;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                ReluInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = 0.5f * (v + Math.Abs(v));
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void ReluInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 64);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_reluInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Relu6(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Relu6(X);

        // f(x) = min(max(x, 0), 6)
        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 64;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                Relu6InnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = 0.5f * (-Math.Abs(v - 6f) + Math.Abs(v) + 6f);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void Relu6InnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 64);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_relu6InnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor LeakyRelu(Tensor X, float alpha)
    {
        if (AreAnyTensorsHalf(X))
            return base.LeakyRelu(X, alpha);

        // f(x) = alpha * x for x < 0, f(x) = x for x >= 0.
        Assert.IsTrue(alpha <= 1);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 64;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                LeakyReluInnerLoop(end, unrollSize, xPtr, oPtr, alpha);

                // from Theano impl
                // https://github.com/Theano/theano/blob/d395439aec5a6ddde8ef5c266fd976412a5c5695/theano/tensor/nnet/nnet.py#L2209-L2251
                float f1 = 0.5f * (1f + alpha);
                float f2 = 0.5f * (1f - alpha);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = f1 * v + f2 * Math.Abs(v);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void LeakyReluInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr, float alpha)
    {
        Assert.AreEqual(unrollSize, 64);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr, alpha);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_leakyReluInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Elu(Tensor X, float alpha)
    {
        if (AreAnyTensorsHalf(X))
            return base.Elu(X, alpha);

        // f(x) = alpha * (exp(x) - 1.) for x < 0, f(x) = x for x >= 0
        // "Fast and Accurate Deep Network Learning by Exponential Linear Units (ELUs)", DA Clevert, 2015
        // https://arxiv.org/abs/1511.07289
        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                EluInnerLoop(end, unrollSize, xPtr, oPtr, alpha);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    if (v <= 0)
                        v = alpha * (Mathf.Exp(v) - 1f);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void EluInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr, float alpha)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr, alpha);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_eluInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor PRelu(Tensor X, Tensor S)
    {
        if (AreAnyTensorsHalf(X, S))
            return base.PRelu(X, S);

        Assert.IsTrue((X.flatWidth == S.flatWidth) || (S.flatWidth == 1));

        // f(x) = x for x >= 0, f(x) = slope*x for x <= 0
        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            float* wPtr = Pin(S).array.AddressAt(Pin(S).offset);
            {
                PReluInnerLoop(end, unrollSize, xPtr, X.length, oPtr, wPtr, S.length);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    float slope = wPtr[i % S.length];
	                v = Mathf.Max(0.0f, v) + slope * Mathf.Min(0.0f, v);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void PReluInnerLoop(int length, int unrollSize, float* xPtr, int xLen, float* oPtr, float* wPtr, int wLen)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, oPtr, xPtr, xLen, wPtr, wLen);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_preluInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Softplus(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Softplus(X);

        // f(x) = 1 / (1 + exp(-x))
        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                SoftplusInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Log(Mathf.Exp(v) + 1f);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void SoftplusInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_softplusInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Sigmoid(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Sigmoid(X);

        // f(x) = 1 / (1 + exp(-x))
        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                SigmoidInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = 1f / (1f + Mathf.Exp(-v));
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void SigmoidInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_sigmoidInnerLoopDelegate);
    }

        /// <inheritdoc/>
    public override Tensor HardSigmoid(Tensor X, float alpha, float beta)
    {
        if (AreAnyTensorsHalf(X))
            return base.HardSigmoid(X, alpha, beta);

        // f(x) = 1 / (1 + exp(-x))
        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                HardSigmoidInnerLoop(end, unrollSize, xPtr, oPtr, alpha, beta);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Max(0.0f, Mathf.Min(1.0f, alpha * v + beta));
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void HardSigmoidInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr, float alpha, float beta)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr, alpha, beta);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_hardsigmoidInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Swish(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Swish(X);

        // f(x) = sigmoid(x) * x = x / (1 + exp(-x))
        // "Searching for Activation Functions". P Ramachandran, 2017
        // https://arxiv.org/abs/1710.05941

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                SwishInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = v / (1f + Mathf.Exp(-v));
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void SwishInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_swishInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Exp(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Exp(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                ExpInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Exp(v);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void ExpInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_expInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Sqrt(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Sqrt(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                SqrtInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Sqrt(v);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void SqrtInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_sqrtInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Tanh(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Tanh(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                TanhInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = MathfEx.Tanh(v);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void TanhInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_tanhInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Acos(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Acos(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                AcosInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Acos(v);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void AcosInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_acosInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Acosh(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Acosh(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                AcoshInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Log(v + Mathf.Sqrt(v*v - 1.0f));
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void AcoshInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_acoshInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Asin(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Asin(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                AsinInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Asin(v);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void AsinInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_asinInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Asinh(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Asinh(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                AsinhInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Log(v + Mathf.Sqrt(v*v + 1.0f));
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void AsinhInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_asinhInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Atan(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Atan(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                AtanInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Atan(v);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void AtanInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_atanInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Atanh(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Atanh(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                AtanhInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = 0.5f * Mathf.Log((1.0f + v)/(1.0f - v));
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void AtanhInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_atanhInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Cos(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Cos(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                CosInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Cos(v);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void CosInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_cosInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Cosh(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Cosh(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                CoshInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = 0.5f * (Mathf.Exp(v) + Mathf.Exp(-v));
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void CoshInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_coshInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Sin(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Sin(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                SinInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Sin(v);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void SinInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_sinInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Sinh(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Sinh(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                SinhInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = 0.5f * (Mathf.Exp(v) - Mathf.Exp(-v));
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void SinhInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_sinhInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Tan(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Tan(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                TanInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    v = Mathf.Tan(v);
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void TanInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_tanInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Erf(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Erf(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                ErfInnerLoop(end, unrollSize, xPtr, oPtr);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    // Abramowitz/Stegun approximations
                    // erf(x) = -erf(-x)
                    float x = Mathf.Abs(v);

                    float p = 0.3275911f;
                    float a1 = 0.254829592f; float a2 = -0.284496736f; float a3 = 1.421413741f;
                    float a4 = -1.453152027f; float a5 = 1.061405429f;

                    float t = 1.0f / (1.0f + p * x);
                    float t2 = t * t;
                    float t3 = t2 * t;
                    float t4 = t3 * t;
                    float t5 = t4 * t;

                    v = Mathf.Sign(v) * (1 - (a1 * t + a2 * t2 + a3 * t3 + a4 * t4 + a5 * t5) * Mathf.Exp(-x * x));
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void ErfInnerLoop(int length, int unrollSize, float* xPtr, float* oPtr)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, xPtr, oPtr);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_erfInnerLoopDelegate);
    }

    private bool CanUseModuloForBroadcasting(TensorShape o, TensorShape a)
    {
        // last to first: dimensions must be equal. if not equal all rest must be 1
        if (o == a)
           return true;

        bool dimensionMismatch = false;
        for (int i = TensorShape.MaxRank - 1; i >= 0; --i)
        {
            if (dimensionMismatch)
            {
                if (a[i] != 1)
                    return false;
            }
            else
            {
                dimensionMismatch = (o[i] != a[i]);
            }
        }

        return true;
    }

    private bool CanUseModuloForBroadcasting(TensorShape o, TensorShape a, TensorShape b)
    {
        return CanUseModuloForBroadcasting(o,a) && CanUseModuloForBroadcasting(o,b);
    }

    private Tensor ApplyElementwiseWithBroadcast(Tensor[] tensors, Func<float,float,float> opRemainder, Action<long> opInnerLoop, Action<long> opInnerLoopNoBroadcast)
    {
        if (!TensorExtensions.AreAllTensorsConvertibleTo4D(tensors) || AreAnyTensorsHalf(tensors))
            throw new NotImplementedException();

        var O = NewTensorLike(tensors, AllocScope.LayerOutput);
        var A = tensors[0];

        unsafe
        {
            float* t0Ptr = Pin(A).array.AddressAt(Pin(A).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                float* aPtr = t0Ptr;
                var aShape = A.shape;

                for (int t = 1; t < tensors.Length; ++t)
                {
                    var B = tensors[t];
                    float* bPtr = Pin(B).array.AddressAt(Pin(B).offset);
                    {
                        //Inner loop
                        const int unrollSize = 4;
                        m_InnerLoop.SetState(unrollSize, oPtr, aPtr, bPtr, O.shape, aShape, B.shape);
                        if (CanUseModuloForBroadcasting(O.shape, aShape, B.shape))
                            Parallel_For(0L, O.length / unrollSize, opInnerLoopNoBroadcast);
                        else
                            Parallel_For(0L, O.length / unrollSize, opInnerLoop);


                        // Remainder
                        for (int i = (O.length / unrollSize) * unrollSize; i < O.length; ++i)
                        {
                            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
                            O.shape.GetPositionsFromIndex(i, ref b0, ref h0, ref w0, ref ch0);
                            oPtr[i] = opRemainder(aPtr[A.shape.IndexWithBroadcast(b0, h0, w0, ch0)], bPtr[B.shape.IndexWithBroadcast(b0, h0, w0, ch0)]);
                        }
                    }

                    aPtr = oPtr;
                    aShape = O.shape;
                }
            }
        }

        return O;
    }

    /// <inheritdoc/>
    public override Tensor Add(Tensor[] tensors)
    {
        if (!TensorExtensions.AreAllTensorsConvertibleTo4D(tensors) || AreAnyTensorsHalf(tensors))
            return base.Add(tensors);

        return ApplyElementwiseWithBroadcast(tensors, m_InnerLoop.m_addOpDelegate, m_InnerLoop.m_addInnerLoopDelegate, m_InnerLoop.m_addInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor Sub(Tensor[] tensors)
    {
        if (!TensorExtensions.AreAllTensorsConvertibleTo4D(tensors) || AreAnyTensorsHalf(tensors))
            return base.Sub(tensors);

        return ApplyElementwiseWithBroadcast(tensors, m_InnerLoop.m_subOpDelegate, m_InnerLoop.m_subInnerLoopDelegate, m_InnerLoop.m_subInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor Mul(Tensor[] tensors)
    {
        if (!TensorExtensions.AreAllTensorsConvertibleTo4D(tensors) || AreAnyTensorsHalf(tensors))
            return base.Mul(tensors);

        return ApplyElementwiseWithBroadcast(tensors, m_InnerLoop.m_mulOpDelegate, m_InnerLoop.m_mulInnerLoopDelegate, m_InnerLoop.m_mulInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor Div(Tensor[] tensors)
    {
        if (!TensorExtensions.AreAllTensorsConvertibleTo4D(tensors) || AreAnyTensorsHalf(tensors))
            return base.Div(tensors);

        return ApplyElementwiseWithBroadcast(tensors, m_InnerLoop.m_divOpDelegate, m_InnerLoop.m_divInnerLoopDelegate, m_InnerLoop.m_divInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor Min(Tensor[] tensors)
    {
        if (!TensorExtensions.AreAllTensorsConvertibleTo4D(tensors) || AreAnyTensorsHalf(tensors))
            return base.Min(tensors);

        return ApplyElementwiseWithBroadcast(tensors, m_InnerLoop.m_minOpDelegate, m_InnerLoop.m_minInnerLoopDelegate, m_InnerLoop.m_minInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor Max(Tensor[] tensors)
    {
        if (!TensorExtensions.AreAllTensorsConvertibleTo4D(tensors) || AreAnyTensorsHalf(tensors))
            return base.Max(tensors);

        return ApplyElementwiseWithBroadcast(tensors, m_InnerLoop.m_maxOpDelegate, m_InnerLoop.m_maxInnerLoopDelegate, m_InnerLoop.m_maxInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor Greater(Tensor A, Tensor B)
    {
        if (!A.shape.Is4D() || !B.shape.Is4D() || AreAnyTensorsHalf(A, B))
            return base.Greater(A,B);

        return ApplyLogicalOperator(A, B, m_InnerLoop.m_greaterOpDelegate, m_InnerLoop.m_greaterInnerLoopDelegate, m_InnerLoop.m_greaterInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor GreaterEqual(Tensor A, Tensor B)
    {
        if (!A.shape.Is4D() || !B.shape.Is4D() || AreAnyTensorsHalf(A, B))
            return base.GreaterEqual(A,B);

        return ApplyLogicalOperator(A, B, m_InnerLoop.m_greaterEqualOpDelegate, m_InnerLoop.m_greaterEqualInnerLoopDelegate, m_InnerLoop.m_greaterEqualInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor Less(Tensor A, Tensor B)
    {
        if (!A.shape.Is4D() || !B.shape.Is4D() || AreAnyTensorsHalf(A, B))
            return base.Less(A,B);

        return ApplyLogicalOperator(A, B, m_InnerLoop.m_lessOpDelegate, m_InnerLoop.m_lessInnerLoopDelegate, m_InnerLoop.m_lessInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor LessEqual(Tensor A, Tensor B)
    {
        if (!A.shape.Is4D() || !B.shape.Is4D() || AreAnyTensorsHalf(A, B))
            return base.LessEqual(A,B);

        return ApplyLogicalOperator(A, B, m_InnerLoop.m_lessEqualOpDelegate, m_InnerLoop.m_lessEqualInnerLoopDelegate, m_InnerLoop.m_lessEqualInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor Equal(Tensor A, Tensor B)
    {
        if (!A.shape.Is4D() || !B.shape.Is4D() || AreAnyTensorsHalf(A, B))
            return base.Equal(A,B);

        return ApplyLogicalOperator(A, B, m_InnerLoop.m_equalOpDelegate, m_InnerLoop.m_equalInnerLoopDelegate, m_InnerLoop.m_equalInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor LogicalOr(Tensor A, Tensor B)
    {
        if (!A.shape.Is4D() || !B.shape.Is4D() || AreAnyTensorsHalf(A, B))
            return base.LogicalOr(A,B);

        return ApplyLogicalOperator(A, B, m_InnerLoop.m_logicalOrOpDelegate, m_InnerLoop.m_logicalOrInnerLoopDelegate, m_InnerLoop.m_logicalOrInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor LogicalAnd(Tensor A, Tensor B)
    {
        if (!A.shape.Is4D() || !B.shape.Is4D() || AreAnyTensorsHalf(A, B))
            return base.LogicalAnd(A,B);

        return ApplyLogicalOperator(A, B, m_InnerLoop.m_logicalAndOpDelegate, m_InnerLoop.m_logicalAndInnerLoopDelegate, m_InnerLoop.m_logicalAndInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor LogicalXor(Tensor A, Tensor B)
    {
        if (!A.shape.Is4D() || !B.shape.Is4D() || AreAnyTensorsHalf(A, B))
            return base.LogicalXor(A,B);

        return ApplyLogicalOperator(A, B, m_InnerLoop.m_logicalXorOpDelegate, m_InnerLoop.m_logicalXorInnerLoopDelegate, m_InnerLoop.m_logicalXorInnerLoopDelegateNoBroadcast);
    }

    /// <inheritdoc/>
    public override Tensor LogicalNot(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.LogicalNot(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                const int unrollSize = 4;
                m_InnerLoop.SetState(unrollSize, xPtr, oPtr);
                Parallel_For(0L, O.length / unrollSize, m_InnerLoop.m_logicalNotInnerLoopDelegate);

                // Remainder
                for (int i = (O.length / unrollSize) * unrollSize; i < O.length; ++i)
                    oPtr[i] = Convert.ToSingle( !Convert.ToBoolean(xPtr[i]) );
            }
        }
        return O;
    }

    /// <inheritdoc/>
    public override Tensor Sign(Tensor X)
    {
        if (AreAnyTensorsHalf(X))
            return base.Sign(X);

        var O = NewTensorLike(X, AllocScope.LayerOutput);

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                const int unrollSize = 4;
                m_InnerLoop.SetState(unrollSize, xPtr, oPtr);
                Parallel_For(0L, O.length / unrollSize, m_InnerLoop.m_signInnerLoopDelegate);

                // Remainder
                for (int i = (O.length / unrollSize) * unrollSize; i < O.length; ++i)
                    oPtr[i] = (xPtr[i] > 0) ? 1.0f : ((xPtr[i] < 0) ? -1.0f : 0.0f);
            }
        }
        return O;
    }

    /// <inheritdoc/>
    public override Tensor Where(Tensor C, Tensor A, Tensor B)
    {
        if (!C.shape.Is4D() || !C.shape.Is4D() || !B.shape.Is4D() || AreAnyTensorsHalf(C,A,B))
            return base.Where(C,A,B);

        var O = NewTensorLike(new [] { C, A, B }, AllocScope.LayerOutput);

        unsafe
        {
            float* cPtr = Pin(C).array.AddressAt(Pin(C).offset);
            float* aPtr = Pin(A).array.AddressAt(Pin(A).offset);
            float* bPtr = Pin(B).array.AddressAt(Pin(B).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                const int unrollSize = 4;
                m_InnerLoop.SetState(unrollSize, oPtr, cPtr, aPtr, bPtr, O.shape, C.shape, A.shape, B.shape);
                if ((O.shape == A.shape) && (O.shape == B.shape))
                    Parallel_For(0L, O.length / unrollSize, m_InnerLoop.m_whereInnerLoopDelegateNoBroadcast);
                else
                    Parallel_For(0L, O.length / unrollSize, m_InnerLoop.m_whereInnerLoopDelegate);

                // Remainder
                for (int i = (O.length / unrollSize) * unrollSize; i < O.length; ++i)
                {
                    int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
                    O.shape.GetPositionsFromIndex(i, ref b0, ref h0, ref w0, ref ch0);
                    oPtr[i] = Convert.ToBoolean(cPtr[C.shape.IndexWithBroadcast(b0, h0, w0, ch0)]) ? aPtr[A.shape.IndexWithBroadcast(b0, h0, w0, ch0)] : bPtr[B.shape.IndexWithBroadcast(b0, h0, w0, ch0)];
                }
            }
        }

        return O;
    }

    private Tensor ApplyLogicalOperator(Tensor A, Tensor B, Func<float,float,float> logicalOpRemainder, Action<long> logicalOpInnerLoop, Action<long> logicalOpInnerLoopNoBroadcast)
    {
        if (!A.shape.Is4D() || !B.shape.Is4D() || AreAnyTensorsHalf(A, B))
            throw new NotImplementedException();

        var O = NewTensorLike(new Tensor[] { A, B }, AllocScope.LayerOutput);

        unsafe
        {
            float* aPtr = Pin(A).array.AddressAt(Pin(A).offset);
            float* bPtr = Pin(B).array.AddressAt(Pin(B).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                const int unrollSize = 4;
                m_InnerLoop.SetState(unrollSize, oPtr, aPtr, bPtr, O.shape, A.shape, B.shape);
                if ((O.shape == A.shape) && (O.shape == B.shape))
                    Parallel_For(0L, O.length / unrollSize, logicalOpInnerLoopNoBroadcast);
                else
                    Parallel_For(0L, O.length / unrollSize, logicalOpInnerLoop);

                // Remainder
                for (int i = (O.length / unrollSize) * unrollSize; i < O.length; ++i)
                {
                    int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
                    O.shape.GetPositionsFromIndex(i, ref b0, ref h0, ref w0, ref ch0);
                    oPtr[i] = logicalOpRemainder(aPtr[A.shape.IndexWithBroadcast(b0, h0, w0, ch0)], bPtr[B.shape.IndexWithBroadcast(b0, h0, w0, ch0)]);
                }
            }
        }

        return O;
    }

    /// <inheritdoc/>
    public override Tensor MatMul(Tensor X, bool xTranspose, Tensor Y, bool yTranspose)
    {
        if (AreAnyTensorsHalf(X,Y))
            return base.MatMul(X, xTranspose, Y, yTranspose);

        Assert.IsTrue(X.dimensions <= 2);
        Assert.IsTrue(Y.dimensions <= 2);

        int xw = X.flatWidth, xh = X.flatHeight;
        int yw = Y.flatWidth, yh = Y.flatHeight;

        if (xTranspose)
        {
            var tmp = xw; xw = xh; xh = tmp;
        }
        if (yTranspose)
        {
            var tmp = yw; yw = yh; yh = tmp;
        }

        Assert.AreEqual(xw, yh);
        var O = NewOutputTensor(X.dataType, new TensorShape(xh, yw));

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* yPtr = Pin(Y).array.AddressAt(Pin(Y).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                // zero-initialize before SGEMM
                UnsafeUtility.MemClear(oPtr, O.length * sizeof(float));

                //D.Log(string.Format("===> X.b[{0}] x Y.w[{1}] * Y.h[{2}] x Y.w[{3}] = O.w[{4}] x O.h[{5}]", X.flatHeight, X.flatWidth, Y.flatHeight, Y.flatWidth, O.batch, O.width));
                blas.SGEMM(
                    xPtr, X.flatHeight, X.flatWidth,
                    yPtr, Y.flatHeight, Y.flatWidth,
                    oPtr, O.flatHeight, O.flatWidth, 16, xTranspose, yTranspose);
            }
        }

        return O;
    }

    /// <inheritdoc/>
    public override Tensor Dense(Tensor X, Tensor W, Tensor B, Layer.FusedActivation fusedActivation)
    {
        if (AreAnyTensorsHalf(X,W,B))
            return base.Dense(X, W, B, fusedActivation);

        //D.Log(string.Format("X = {0}", X.shape));
        Assert.IsTrue(W.dimensions <= 2);
        Assert.AreEqual(B.flatWidth, B.length);
        Assert.AreEqual(B.flatWidth, W.flatWidth);
        Assert.AreEqual(X.flatWidth, W.flatHeight);
        var O = NewTensorForFusedActivation(X.dataType, new TensorShape(X.flatHeight, W.flatWidth), fusedActivation);

        var pinX = Pin(X);
        var pinW = Pin(W);
        var pinB = Pin(B);
        var pinO = Pin(O, uploadCache:false);

        unsafe
        {
            float* xPtr = pinX.array.AddressAt(pinX.offset);
            float* wPtr = pinW.array.AddressAt(pinW.offset);
            float* bPtr = pinB.array.AddressAt(pinB.offset);
            float* oPtr = pinO.array.AddressAt(pinO.offset);
            {
                var count = B.flatWidth;

                for (int i = 0; i < O.flatHeight; i++)
                {
                    UnsafeUtility.MemCpy(oPtr + pinO.offset + i * count, bPtr, count * sizeof(float));
                }

                //X.Print(); W.Print();
                blas.SGEMM(
                    xPtr, X.flatHeight, X.flatWidth,
                    wPtr, W.flatHeight, W.flatWidth,
                    oPtr, O.flatHeight, O.flatWidth, 16);
            }
        }

        return ApplyFusedActivation(O, fusedActivation);
    }

    /// <summary>
    /// Apply fused activation
    /// </summary>
    /// <param name="X">input</param>
    /// <param name="fusedActivation">fused activation type</param>
    /// <returns>output `Tensor`</returns>
    /// <exception cref="NotImplementedException">thrown if unsupported activation type encountered</exception>
    protected Tensor ApplyFusedActivation(Tensor X, Layer.FusedActivation fusedActivation)
    {
        switch (fusedActivation)
        {
            case Layer.FusedActivation.None:
                return X;
            case Layer.FusedActivation.Relu:
                return Relu(X);
            case Layer.FusedActivation.Tanh:
                return Tanh(X);
            case Layer.FusedActivation.Softplus:
                return Softplus(X);
            case Layer.FusedActivation.Sigmoid:
                return Sigmoid(X);
            case Layer.FusedActivation.Relu6:
                return Relu6(X);
            case Layer.FusedActivation.Swish:
                return Swish(X);
            case Layer.FusedActivation.Neg:
                return Neg(X);
            case Layer.FusedActivation.Sqrt:
                return Sqrt(X);
            case Layer.FusedActivation.Exp:
                return Exp(X);
            case Layer.FusedActivation.Log:
                return Log(X);
            case Layer.FusedActivation.Acos:
                return Acos(X);
            case Layer.FusedActivation.Acosh:
                return Acosh(X);
            case Layer.FusedActivation.Asin:
                return Asin(X);
            case Layer.FusedActivation.Asinh:
                return Asinh(X);
            case Layer.FusedActivation.Atan:
                return Atan(X);
            case Layer.FusedActivation.Atanh:
                return Atanh(X);
            case Layer.FusedActivation.Cos:
                return Cos(X);
            case Layer.FusedActivation.Cosh:
                return Cosh(X);
            case Layer.FusedActivation.Sin:
                return Sin(X);
            case Layer.FusedActivation.Sinh:
                return Sinh(X);
            case Layer.FusedActivation.Tan:
                return Tan(X);
            case Layer.FusedActivation.Erf:
                return Erf(X);
            default:
                throw new NotImplementedException();
        }
    }

    /// <inheritdoc/>
    public override Tensor MaxPool2D(Tensor X, int[] pool, int[] stride, int[] pad)
    {
        if (AreAnyTensorsHalf(X))
            return base.MaxPool2D(X, pool, stride, pad);

        Assert.IsTrue(X.shape.Is4D());
        Assert.AreEqual(pool.Length, 2);
        Assert.AreEqual(stride.Length, 2);
        Assert.AreEqual(pad.Length, 4);

        var O = NewOutputTensor(X.dataType, X.shape.ApplyPool(pool, stride, pad));

        int xnMult = X.height * X.width * X.channels;
        int xyMult = X.width * X.channels;
        int xxMult = X.channels;

        int onMult = O.height * O.width * O.channels;
        int oyMult = O.width * O.channels;
        int oxMult = O.channels;

        int oBatch = O.batch;
        int oHeight = O.height;
        int oWidth = O.width;
        int oChannels = O.channels;
        int xHeight = X.height;
        int xWidth = X.width;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                MaxPool2DInnerLoop(pool, stride, pad,
                    xHeight, xWidth, xPtr, xnMult, xyMult, xxMult,
                    oBatch, oHeight, oWidth, oChannels, oPtr, onMult, oyMult, oxMult);
            }
        }

        return O;
    }

    private static unsafe void MaxPool2DInnerLoop(int[] pool, int[] stride, int[] pad,
        int xHeight, int xWidth, float* xPtr, int xnMult, int xyMult, int xxMult,
        int oBatch, int oHeight, int oWidth, int oChannels, float* oPtr, int onMult, int oyMult, int oxMult)
    {
        Parallel.For(0, oBatch, n =>
        {
            for (var y = 0; y < oHeight; ++y)
            for (var x = 0; x < oWidth; ++x)
            for (var c = 0; c < oChannels; ++c)
            {
                float maxVal = float.MinValue;
                for (int dy = 0; dy < pool[1]; ++dy)
                    for (int dx = 0; dx < pool[0]; ++dx)
                    {
                        int oy = y * stride[1] + dy - pad[1];
                        int ox = x * stride[0] + dx - pad[0];

                        if (oy < 0) continue;
                        if (oy >= xHeight) continue;
                        if (ox < 0) continue;
                        if (ox >= xWidth) continue;

                        float v = xPtr[n * xnMult + oy * xyMult + ox * xxMult + c];
                        maxVal = Mathf.Max(v, maxVal);
                    }
                oPtr[n * onMult + y * oyMult + x * oxMult + c] = maxVal;
            }
        });
    }

    /// <inheritdoc/>
    public override Tensor AvgPool2D(Tensor X, int[] pool, int[] stride, int[] pad)
    {
        if (AreAnyTensorsHalf(X))
            return base.AvgPool2D(X, pool, stride, pad);

        Assert.IsTrue(X.shape.Is4D());
        Assert.AreEqual(pool.Length, 2);
        Assert.AreEqual(stride.Length, 2);
        Assert.AreEqual(pad.Length, 4);

        var O = NewOutputTensor(X.dataType, X.shape.ApplyPool(pool, stride, pad));

        int xnMult = X.height * X.width * X.channels;
        int xyMult = X.width * X.channels;
        int xxMult = X.channels;

        int onMult = O.height * O.width * O.channels;
        int oyMult = O.width * O.channels;
        int oxMult = O.channels;

        int oBatch = O.batch;
        int oHeight = O.height;
        int oWidth = O.width;
        int oChannels = O.channels;
        int xHeight = X.height;
        int xWidth = X.width;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                AvgPool2DInnerLoop(pool, stride, pad,
                    xHeight, xWidth, xPtr, xnMult, xyMult, xxMult,
                    oBatch, oHeight, oWidth, oChannels, oPtr, onMult, oyMult, oxMult);
            }
        }

        return O;
    }

    private static unsafe void AvgPool2DInnerLoop(int[] pool, int[] stride, int[] pad,
        int xHeight, int xWidth, float* xPtr, int xnMult, int xyMult, int xxMult,
        int oBatch, int oHeight, int oWidth, int oChannels, float* oPtr, int onMult, int oyMult, int oxMult)
    {
        Parallel.For(0, oBatch, n =>
        {
            for (var y = 0; y < oHeight; ++y)
            for (var x = 0; x < oWidth; ++x)
            for (var c = 0; c < oChannels; ++c)
            {
                float accum = 0.0f;
                float counter = 0.0f;
                for (int dy = 0; dy < pool[1]; ++dy)
                    for (int dx = 0; dx < pool[0]; ++dx)
                    {
                        int oy = y * stride[1] + dy - pad[1];
                        int ox = x * stride[0] + dx - pad[0];

                        if (oy < 0) continue;
                        if (oy >= xHeight) continue;
                        if (ox < 0) continue;
                        if (ox >= xWidth) continue;

                        float v = xPtr[n * xnMult + oy * xyMult + ox * xxMult + c];
                        accum += v;
                        ++counter;
                    }
                oPtr[n * onMult + y * oyMult + x * oxMult + c] = accum / counter;
            }
        });
    }

    /// <inheritdoc/>
    public override Tensor GlobalMaxPool2D(Tensor X)
    {
        return MaxPool2D(X, new[] {X.width, X.height}, new[] {1, 1}, new[] {0, 0, 0, 0});
    }

    /// <inheritdoc/>
    public override Tensor GlobalAvgPool2D(Tensor X)
    {
        return AvgPool2D(X, new[] {X.width, X.height}, new[] {1, 1}, new[] {0, 0, 0, 0});
    }

    /// <inheritdoc/>
    public override Tensor Conv2D(Tensor X, Tensor K, Tensor B, int[] stride, int[] pad, Layer.FusedActivation fusedActivation)
    {
        // Basic Im2Col+SGEMM implementation for reference:
        //
        // var unrolledX = Im2Col(X, K.shape, stride, pad);
        // var flatK     = K.Reshape(new TensorShape(unrolledX.flatWidth, K.kernelCount));
        // var flatO     = Dense(unrolledX, flatK, B);
        // return flatO.Reshape(X.shape.ApplyKernel(K.shape, stride, pad));

        // Memory efficient implementation of Im2Col+SGEMM
        // Requires temporary tensor of input shape (X) divided by stride
        //  = sizeof(X) / (stride[0] * stride[1])
        //
        // Performance measurements:
        // (MacBookPro2016)
        //  Standalone
        //   VGG@1   338ms   Dense 23.2ms ( 7%), Conv2D 230ms (68%): Broadcast 5.9ms ( 3%), Im2Col 33.9ms (15%), GEMM 188.7ms (82%)    mono:0.57GB
        //   CNN@256 180ms   Dense  3.7ms ( 2%), Conv2D 118ms (66%): Broadcast 6.3ms ( 5%), Im2Col 30.7ms (26%), GEMM  81.2ms (69%)    mono:0.15GB
        //   MOB@1    65ms   Dpthw 12.6ms (19%), Conv2D  11ms (17%): Broadcast 1.3ms (12%), Im2Col  0.4ms ( 4%), GEMM   8.5ms (77%)    mono:0.025-0.03GB
        //  Editor
        //   VGG@1   502ms   Dense 24.6ms ( 5%), Conv2D 210ms (42%): Broadcast 4.9ms ( 2%), Im2Col 33.0ms (16%), GEMM 170.8ms (81%)
        //   CNN@256 266ms   Dense  3.2ms ( 1%), Conv2D 119ms (45%): Broadcast 7.0ms ( 6%), Im2Col 33.0ms (27%), GEMM  78.4ms (65%)
        //   MOB@1   131ms   Dpthw 43.6ms (33%), Conv2D  11ms ( 8%): Broadcast 1.2ms (10%), Im2Col  0.6ms ( 5%), GEMM   8.1ms (74%)
        //   CNN@16  17ms    Dense  1.1ms ( 6%), Conv2D   6ms (35%): Broadcast .34ms ( 6%), Im2Col 2.23ms (37%), GEMM   3.4ms (57%)
        //  Standalone log measurements
        //   VGG          <<<Exec #64:  338.3 ms, cpu: 338.0 ms, avg: 338.0 ms, result:OK
        //   CNN          <<<Exec #256: 162.6 ms, cpu: 201.1 ms, avg: 201.1 ms, result:OK
        //   Mobilenet    <<<Exec #64:   63.1 ms, cpu: 65.2 ms,  avg:  65.2 ms, result:OK
        //  Editor log measurements
        //   VGG          <<<Exec #10:  483.9 ms, cpu: 496.8 ms, avg: 496.8 ms, result:OK
        //   CNN@256      <<<Exec #10:  251.3 ms, cpu: 253.1 ms, avg: 253.1 ms, result:OK
        //   Mobilenet    <<<Exec #10:  129.3 ms, cpu: 133.6 ms, avg: 133.6 ms, result:OK
        //   CNN@16       <<<Exec #10:   17.0 ms, cpu:  16.2 ms, avg:  16.2 ms, result:OK

        if (AreAnyTensorsHalf(X,K,B))
            return base.Conv2D(X, K, B, stride, pad, fusedActivation);
        else
            return Conv2DUsingIm2ColSlicedHelper(X, K, B, stride, pad, fusedActivation);

        // Slightly faster, but much more memory
        // Requires temporary tensor of input shape (X) multiple of kernel width & height and divided by stride
        //   = sizeof(X) * K.kernelWidth * K.kernelHeight / (stride[0] * stride[1])

        // Performance measurements:
        // (MacBookPro2016)
        //  Standalone
        //   VGG@1   326ms   Dense 28.5ms ( 2%), Conv2D 232ms (71%): Broadcast 5.4ms ( 2%), Im2Col 55.6ms (24%), GEMM 169.9ms (73%)    mono:0.65GB
        //   CNN@256 148ms   Dense  3.4ms ( 2%), Conv2D  87ms (59%): Broadcast 5.6ms ( 6%), Im2Col 18.9ms (22%), GEMM  62.7ms (72%)    mono:0.4GB
        //  Editor
        //   VGG@1   484ms   Dense 26.3ms ( 5%), Conv2D 208ms (43%): Broadcast 4.5ms ( 2%), Im2Col 49.3ms (24%), GEMM 153.4ms (74%)
        //   CNN@256 218ms   Dense  4.3ms ( 2%), Conv2D  84ms (39%): Broadcast 5.0ms ( 6%), Im2Col 27.6ms (13%), GEMM  51.7ms (62%)
        //   CNN@16   17ms   Dense  1.2ms ( 7%), Conv2D   7ms (41%): Broadcast 0.4ms ( 6%), Im2Col  2.5ms (36%), GEMM   3.7ms (53%)
        //  Standalone log measurements
        //   VGG          <<<Exec #64:  326.0 ms, cpu: 362.9 ms, avg: 362.9 ms, result:OK
        //   CNN          <<<Exec #256: 140.1 ms, cpu: 148.3 ms, avg: 148.3 ms, result:OK
        //  Editor log measurements
        //   VGG          <<<Exec #10:  504.1 ms, cpu: 485.7 ms, avg: 485.7 ms, result:OK
        //   CNN@256      <<<Exec #10:  219.2 ms, cpu: 227.8 ms, avg: 227.9 ms, result:OK
        //   CNN@16       <<<Exec #100:  17.4 ms, cpu:  16.9 ms, avg:  16.9 ms, result:OK

        // return Conv2DUsingIm2Col(X, K, B, stride, pad);

        // Performance measurements for the old version of Conv2D prior to Im2Col+SGEMM implementation:
        // (MacBookPro2016)
        //  Standalone
        //   VGG@1   50sec(???)
        //   CNN@256  1033.0ms    Conv2D 943.0ms
        //   CNN@16     81.5ms    Conv2D  65.9ms
        //  Editor
        //   CNN@16    440.0ms    Conv2D 429.4ms
    }

    /* Not used anymore, see Conv2DUsingIm2ColSlicedHelper*/
    /*Tensor Conv2DUsingIm2Col(Tensor X, Tensor K, Tensor B, int[] stride, int[] pad)
    {
        Assert.IsTrue(X.shape.Is4D());
        Assert.AreEqual(X.channels, K.kernelDepth);
        Assert.AreEqual(K.kernelCount, B.flatWidth);
        Assert.AreEqual(B.flatWidth, B.length);
        Assert.AreEqual(stride.Length, 2);
        Assert.AreEqual(pad.Length, 4);

        var kernelWidth = K.kernelWidth;
        var kernelHeight = K.kernelHeight;
        var inChannels = K.kernelDepth;
        var outChannels = K.kernelCount;
        var batch = X.batch;
        Assert.AreEqual(inChannels, X.channels);

        bool pointwiseConvolution = kernelWidth == 1 && kernelHeight == 1 &&                    // 1x1 kernel
                                    stride[0] == 1 && stride[1] == 1 &&                         // no strides
                                    pad[0] == 0 && pad[1] == 0 && pad[2] == 0 && pad[3] == 0;   // no padding

        var O = NewOutputTensor(X.shape.ApplyKernel(K.shape, stride, pad));
        var T = pointwiseConvolution ? null:                                                    // pointwise convolution is just O=X*K, we can completely skip Im2Col()
                NewTempTensor(new TensorShape(O.batch, O.height, O.width, kernelHeight * kernelWidth * inChannels)); // holds results of Im2Col(X)

        var outElements = O.batch * O.height * O.width;

        var inStrideBatch = X.height * X.width * X.channels;
        var inStrideHeight = X.width * X.channels;
        var inStrideWidth = X.channels;
        var inWidth = X.width;
        var inHeight = X.height;

        Assert.AreEqual(O.batch, batch);
        Assert.AreEqual(O.channels, B.flatWidth);
        Assert.AreEqual(O.channels, outChannels);

        unsafe
        {
            // input & constants
            var pinnedX  = Pin(X);
            var pinnedK  = Pin(K);
            var pinnedB  = Pin(B);

            // temporary slice
            var pinnedT  = (pointwiseConvolution) ? pinnedX  : Pin(T);

            // output
            var pinnedO = Pin(O, uploadCache: false);

            float* xPtr = pinnedX.array.AddressAt(pinnedX.offset);
            float* tPtr = pinnedT.array.AddressAt(pinnedT.offset);
            float* kPtr = pinnedK.array.AddressAt(pinnedK.offset);
            float* bPtr = pinnedB.array.AddressAt(pinnedB.offset);
            float* oPtr = pinnedO.array.AddressAt(pinnedO.offset);
            {
                // O = broadcast(B)
                Profiler.BeginSample("Conv2D_Im2Col.BroadcastB");
                UnsafeUtility.MemCpyReplicate(destination: oPtr,
                                              source:      bPtr,
                                              size:        outChannels * sizeof(float),
                                              count:       outElements);
                Profiler.EndSample();

                // T = im2col(X)
                if (!pointwiseConvolution)
                {
                    Profiler.BeginSample("Conv2D_Im2Col.Im2Col");
                    var tStrideBatch = T.height * T.width * T.channels;
                    var tHeight = T.height;
                    var tWidth = T.width;
                    Im2ColInnerLoop(stride, pad, batch,
                        xPtr, inHeight, inWidth, inChannels, inStrideBatch, inStrideHeight, inStrideWidth,
                        tPtr,  tHeight,  tWidth,              tStrideBatch,
                        kernelHeight, kernelWidth);
                    Profiler.EndSample();
                }

                // O += T * K
                Profiler.BeginSample("Conv2D_Im2Col.SGEMM");
                var unrolledChannels = kernelHeight * kernelWidth * inChannels;
                blas.SGEMM(
                    tPtr, outElements, unrolledChannels,
                    kPtr, unrolledChannels, outChannels,
                    oPtr, outElements, outChannels, 16);
                Profiler.EndSample();
            }
        }

        T?.Dispose();

        return O;//TODO need to handle fusedactivation
    }*/

    /*
    private static unsafe void Im2ColInnerLoop(int[] stride, int[] pad, int batch,
        float* xPtr, int xHeight, int xWidth, int xChannels, int xStrideBatch, int xStrideHeight, int xStrideWidth,
        float* oPtr, int oHeight, int oWidth, int oStrideBatch,
        int kernelHeight, int kernelWidth)
    {
        Parallel.For(0, batch, n =>
        {
            var to = oPtr + n * oStrideBatch;
            for (var y = 0; y < oHeight; ++y)
                for (var x = 0; x < oWidth; ++x)
                    for (int dy = 0; dy < kernelHeight; ++dy)
                        for (int dx = 0; dx < kernelWidth; ++dx)
                        {
                            int readX = x * stride[0] + dx - pad[0];
                            int readY = y * stride[1] + dy - pad[1];

                            if (readX < 0 ||
                                readY < 0 ||
                                readX >= xWidth ||
                                readY >= xHeight)
                            {
                                // pad-0
                                UnsafeUtility.MemClear(destination: to,
                                                       size:        xChannels * sizeof(float));
                                to += xChannels;
                            }
                            else
                            {
                                var from = xPtr + n * xStrideBatch + readY * xStrideHeight + readX * xStrideWidth;
                                UnsafeUtility.MemCpy(destination: to,
                                                     source:      from,
                                                     size:        xChannels * sizeof(float));
                                to += xChannels;
                            }
                        }
        });
    }*/

    static internal int SafeIntDivCeil(int v, int div)
    {
        if (div == 0)
            return v;
        return (v + div - 1) / div;
    }

    private Tensor Conv2DUsingIm2ColSlicedHelper(Tensor X, Tensor K, Tensor B, int[] stride, int[] pad, Layer.FusedActivation fusedActivation)
    {
        Assert.IsFalse(AreAnyTensorsHalf(X,K,B));
        Assert.IsTrue(X.shape.Is4D());
        Assert.AreEqual(X.channels, K.kernelDepth);
        Assert.AreEqual(K.kernelCount, B.flatWidth);
        Assert.AreEqual(B.flatWidth, B.length);
        Assert.AreEqual(stride.Length, 2);
        Assert.AreEqual(pad.Length, 4);

        var kernelWidth = K.kernelWidth;
        var kernelHeight = K.kernelHeight;
        var inChannels = K.kernelDepth;
        var outChannels = K.kernelCount;
        var batch = X.batch;

        bool pointwiseConvolution = kernelWidth == 1 && kernelHeight == 1 &&                    // 1x1 kernel
                                    stride[0] == 1 && stride[1] == 1 &&                         // no strides
                                    pad[0] == 0 && pad[1] == 0 && pad[2] == 0 && pad[3] == 0;   // no padding

        var O = NewTensorForFusedActivation(X.dataType, X.shape.ApplyKernel(K.shape, stride, pad), fusedActivation);
        var T = pointwiseConvolution ? null:                       // pointwise convolution is just O=X*K, we can completely skip Im2Col()
                NewTempTensor(X.dataType, new TensorShape(O.batch, O.height, O.width, inChannels)); // holds slice of Im2Col(X)

        var outElements = O.batch * O.height * O.width;

        var xStrideBatch = X.height * X.width * X.channels;
        var xStrideHeight = X.width * X.channels;
        var xStrideWidth = X.channels;
        var xWidth = X.width;
        var xHeight = X.height;

        Assert.AreEqual(O.batch, batch);
        Assert.AreEqual(O.channels, B.flatWidth);
        Assert.AreEqual(O.channels, outChannels);

        unsafe
        {
            // input & constants
            var pinnedX  = Pin(X);
            var pinnedK  = Pin(K);
            var pinnedB  = Pin(B);

            // temporary slice
            var pinnedT  = (pointwiseConvolution) ? pinnedX  : Pin(T);

            // output
            var pinnedO = Pin(O, uploadCache: false);

            float* xPtr = pinnedX.array.AddressAt(pinnedX.offset);
            float* tPtr = pinnedT.array.AddressAt(pinnedT.offset);
            float* kPtr = pinnedK.array.AddressAt(pinnedK.offset);
            float* bPtr = pinnedB.array.AddressAt(pinnedB.offset);
            float* oPtr = pinnedO.array.AddressAt(pinnedO.offset);
            {
                // O = broadcast(B)
                Profiler.BeginSample("Conv2D_Sliced.BroadcastB");
                UnsafeUtility.MemCpyReplicate(destination: oPtr,
                                              source:      bPtr,
                                              size:        outChannels * sizeof(float),
                                              count:       outElements);
                Profiler.EndSample();

                // We can solve convolution by iteratively accumulating
                // matrix multiplication of X' and K' for each positon in kernel where:
                //  X' is input X repeatedly shifted according to kernel position,
                //  K' is slice of weights K according to kernel position.
                //
                // Pseudocode:
                //  X :: Input
                //  T :: Temporary
                //  K :: Kernel
                //  O :: Output
                //  foreach ky in kernelHeight:
                //      foreach kx in kernelWidth:
                //          Temporary = shift(Input, horizontal_shift = kx, vertical_shift = ky)
                //          Temporary = pad(Temporary)
                //          Temporary = stride(Temporary)
                //          Output += Temporary * Kernel[dy, dx, :, :]
                //
                // Note for functions above that:
                //  1) shift() can be implemented by copying data from n to T in a linear fashion.
                //  2) stride() can be implemented by copying data every Nth pixel in a linear fashion.
                //  3) pad() can be optimized for top and bottom of the tensor by writing 0s across the whole row.

                // O += conv(X, K)
                float* wPtr = kPtr;
                for (int dy = 0; dy < kernelHeight; ++dy)
                    for (int dx = 0; dx < kernelWidth; ++dx)
                    {
                        if (!pointwiseConvolution)
                        {
                            Profiler.BeginSample("Conv2D_Sliced.Im2ColSlice");

                            var tStrideBatch = T.height * T.width * T.channels;
                            var tStrideHeight = T.width * T.channels;
                            var tHeight = T.height;
                            var tWidth = T.width;

                            var offsetX = dx - pad[0];
                            var offsetY = dy - pad[1];

                            var strideX = stride[0];
                            var strideY = stride[1];

                            var firstPixel =            0 * strideX + offsetX;
                            var lastPixel  = (tWidth - 1) * strideX + offsetX;
                            int numberOfPixelsToPadLeft  = SafeIntDivCeil(Math.Max(0, 0 - firstPixel               ), strideX);   // count(x * stride[0] + offsetX < 0)
                            int numberOfPixelsToPadRight = SafeIntDivCeil(Math.Max(0,      lastPixel - (xWidth - 1)), strideX);   // count(x * stride[0] + offsetX >= xWidth)
                            int numberOfPixelsToSkipFromInputRow = (offsetX >= 0 || strideX == 0) ? offsetX :                     // strideX == 0 protects against div-by-zero
                                lastPixel % strideX;                                                                              // first(x * stride[0] + offsetX >= 0) == (xWidth * stride[0] + offsetX) % stride[0]
                            int numberOfPixelsToCopyFromInputRow = tWidth - numberOfPixelsToPadLeft - numberOfPixelsToPadRight;

                            if (UnityEngine.Debug.isDebugBuild) // only to Assert correctness of the values above
                            {
                                // validate above calculations with alternative approach
                                int assertNumberOfPixelsToPadLeft = 0;
                                int assertNumberOfPixelsToPadRight = 0;
                                int assertNumberOfPixelsToSkipFromInputRow = 0;
                                for (var x = 0; x < tWidth; ++x)
                                {
                                    var readX = x * strideX + offsetX;
                                    if (readX < 0)
                                        assertNumberOfPixelsToPadLeft++;
                                    else
                                    {
                                        assertNumberOfPixelsToSkipFromInputRow = readX;
                                        break;
                                    }
                                }
                                for (var x = tWidth - 1; x >= 0; --x)
                                {
                                    var readX = x * strideX + offsetX;
                                    if (readX >= xWidth)
                                        assertNumberOfPixelsToPadRight++;
                                    else
                                        break;
                                }
                                int assertNumberOfPixelsToCopyFromInputRow = tWidth - assertNumberOfPixelsToPadLeft - assertNumberOfPixelsToPadRight;

                                Assert.AreEqual(numberOfPixelsToPadLeft,            assertNumberOfPixelsToPadLeft);
                                Assert.AreEqual(numberOfPixelsToPadRight,           assertNumberOfPixelsToPadRight);
                                Assert.AreEqual(numberOfPixelsToSkipFromInputRow,   assertNumberOfPixelsToSkipFromInputRow);
                                Assert.AreEqual(numberOfPixelsToCopyFromInputRow,   assertNumberOfPixelsToCopyFromInputRow);
                            }

                            Assert.IsTrue(numberOfPixelsToPadLeft >= 0);
                            Assert.IsTrue(numberOfPixelsToPadRight >= 0);
                            Assert.IsTrue(numberOfPixelsToCopyFromInputRow >= 0);
                            Assert.IsTrue(numberOfPixelsToSkipFromInputRow >= 0);
                            Assert.IsTrue(numberOfPixelsToPadLeft + numberOfPixelsToPadRight <= tWidth);
                            Assert.IsTrue(numberOfPixelsToSkipFromInputRow <= xWidth);
                            Assert.IsTrue(numberOfPixelsToCopyFromInputRow <= xWidth);
                            Assert.AreEqual(numberOfPixelsToPadLeft + numberOfPixelsToCopyFromInputRow + numberOfPixelsToPadRight, tWidth);

                            // extra clamp for safety since we are in the unsafe code block
                            numberOfPixelsToPadLeft          = Math.Min(Math.Max(0, numberOfPixelsToPadLeft), tWidth);
                            numberOfPixelsToPadRight         = Math.Min(Math.Max(0, numberOfPixelsToPadRight), tWidth - numberOfPixelsToPadLeft);
                            numberOfPixelsToSkipFromInputRow = Math.Min(Math.Max(0, numberOfPixelsToSkipFromInputRow), xWidth);
                            numberOfPixelsToCopyFromInputRow = Math.Min(Math.Max(0, numberOfPixelsToCopyFromInputRow), xWidth - numberOfPixelsToSkipFromInputRow);

                            for (var n = 0; n < batch; ++n)
                                for (var y = 0; y < tHeight; ++y)
                                {
                                    var readY = strideY * y  + offsetY;
                                    var from = xPtr + n * xStrideBatch + readY * xStrideHeight + numberOfPixelsToSkipFromInputRow * xStrideWidth;
                                    var to   = tPtr + n * tStrideBatch +     y * tStrideHeight;

                                    if (readY < 0 ||
                                        readY >= xHeight)
                                    {
                                        // pad-0 top or bottom line, len = tWidth
                                        UnsafeUtility.MemClear(destination: to,
                                                               size:        inChannels * tWidth * sizeof(float));
                                        to += inChannels * tWidth;
                                    }
                                    else
                                    {
                                        // pad-0 left, len = numberOfPixelsToPadLeft
                                        UnsafeUtility.MemClear(destination: to,
                                                               size:        inChannels * numberOfPixelsToPadLeft * sizeof(float));
                                        to += inChannels * numberOfPixelsToPadLeft;

                                        // copy from X with stride, if necessary
                                        if (strideX == 1)
                                        {
                                            UnsafeUtility.MemCpy(destination: to,
                                                                 source:      from,
                                                                 size:        inChannels * numberOfPixelsToCopyFromInputRow * sizeof(float));
                                            to += inChannels * numberOfPixelsToCopyFromInputRow;
                                        }
                                        else
                                        {
                                            UnsafeUtility.MemCpyStride(destination: to,     destinationStride:           inChannels * sizeof(float),
                                                                       source:      from,   sourceStride:      strideX * inChannels * sizeof(float),
                                                                       elementSize: inChannels * sizeof(float),
                                                                       count:       numberOfPixelsToCopyFromInputRow);
                                            to += inChannels * numberOfPixelsToCopyFromInputRow;
                                        }

                                        // pad-0 right, len = numberOfPixelsToPadRight
                                        UnsafeUtility.MemClear(destination: to,
                                                               size:        inChannels * numberOfPixelsToPadRight * sizeof(float));
                                        to += inChannels * numberOfPixelsToPadRight;
                                    }
                                }
                            Profiler.EndSample();
                        }

                        Profiler.BeginSample("Conv2D_Sliced.SGEMM");
                        // O += slice(im2col(X)) * slice(K)
                        blas.SGEMM(
                            tPtr, outElements, inChannels,
                            wPtr, inChannels, outChannels,
                            oPtr, outElements, outChannels, 16);

                        wPtr += inChannels * outChannels;
                        Profiler.EndSample();
                    }
            }
        }

        T?.Dispose();

        return ApplyFusedActivation(O, fusedActivation);
    }

    /// <inheritdoc/>
    public override Tensor DepthwiseConv2D(Tensor X, Tensor K, Tensor B, int[] stride, int[] pad, Layer.FusedActivation fusedActivation)
    {
        if (K.kernelDepth != 1 || AreAnyTensorsHalf(X,K,B))
            return base.DepthwiseConv2D(X, K, B, stride, pad, fusedActivation);

        Assert.IsTrue(X.shape.Is4D());
        Assert.AreEqual(K.kernelDepth, 1);
        Assert.AreEqual(K.kernelCount, X.channels);
        Assert.AreEqual(K.kernelCount, B.flatWidth);
        Assert.AreEqual(B.flatWidth, B.length);
        Assert.AreEqual(stride.Length, 2);
        Assert.AreEqual(pad.Length, 4);

        // ONNX: (M x C/group x kH x kW)
        // TF: [H, W, in_channels, channel_multiplier]

        // TF pseudocode:
        // output[b, i, j, k * channel_multiplier + q] =
        // sum_{di, dj}
        //      input [b, i + di, j + dj, k] *
        //      filter[di, dj, k, q] *

        var O = NewTensorForFusedActivation(X.dataType, X.shape.ApplyKernel(K.shape, stride, pad), fusedActivation);

        int xnMult = X.height * X.width * X.channels;
        int xyMult = X.width * X.channels;
        int xxMult = X.channels;

        int kyMult = K.height * K.width * K.channels;
        int kxMult = K.width * K.channels;

        int onMult = O.height * O.width * O.channels;
        int oyMult = O.width * O.channels;
        int oxMult = O.channels;

        int oBatch = O.batch;
        int oHeight = O.height;
        int oWidth = O.width;
        int kKernelCount = K.kernelCount;
        int kKernelHeight = K.kernelHeight;
        int kKernelWidth = K.kernelWidth;
        int xHeight = X.height;
        int xWidth = X.width;
        int xChannels = X.channels;

        unsafe
        {

            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* kPtr = Pin(K).array.AddressAt(Pin(K).offset);
            float* bPtr = Pin(B).array.AddressAt(Pin(B).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            {
                DepthwiseConv2DInnerLoop(stride, pad, oBatch, oHeight, oWidth, kKernelCount, bPtr, kKernelHeight, kKernelWidth,
                    xHeight, xWidth, xChannels, xPtr, xnMult, xyMult, xxMult, kPtr, kyMult, kxMult,
                    oPtr, onMult, oyMult, oxMult);
            }
        }

        return ApplyFusedActivation(O, fusedActivation);
    }

    // private static unsafe void DepthwiseConv2DInnerLoop(int[] stride, int[] pad, int oBatch, int oHeight, int oWidth, int kKernelCount,
    //     float* bPtr, int kKernelHeight, int kKernelWidth, int xHeight, int xWidth, int xChannels, float* xPtr,
    //     int xnMult, int xyMult, int xxMult, float* kPtr, int kyMult, int kxMult, float* oPtr, int onMult,
    //     int oyMult, int oxMult)
    // {
    //     Parallel.For(0, oBatch, n =>
    //     {
    //         for (var y = 0; y < oHeight; ++y)
    //         for (var x = 0; x < oWidth; ++x)
    //         for (var k = 0; k < kKernelCount; ++k)
    //         {
    //             float v = bPtr[k];
    //             for (int dy = 0; dy < kKernelHeight; ++dy)
    //             {
    //                 for (int dx = 0; dx < kKernelWidth; ++dx)
    //                 {
    //                     int oy = y * stride[1] + dy - pad[1];
    //                     int ox = x * stride[0] + dx - pad[0];

    //                     if (oy < 0) continue;
    //                     if (oy >= xHeight) continue;
    //                     if (ox < 0) continue;
    //                     if (ox >= xWidth) continue;

    //                     float xv = xPtr[n * xnMult + oy * xyMult + ox * xxMult + k];
    //                     float kv = kPtr[dy * kyMult + dx * kxMult              + k];

    //                     v += xv * kv;
    //                 }
    //             }

    //             oPtr[n * onMult + y * oyMult + x * oxMult + k] = v;
    //         }
    //     });
    // }

    // private static unsafe void DepthwiseConv2DInnerLoop(int[] stride, int[] pad, int oBatch, int oHeight, int oWidth, int kKernelCount,
    //     float* bPtr, int kKernelHeight, int kKernelWidth, int xHeight, int xWidth, int xChannels, float* xPtr,
    //     int xnMult, int xyMult, int xxMult, float* kPtr, int kyMult, int kxMult, float* oPtr, int onMult,
    //     int oyMult, int oxMult)
    // {
    //     Parallel.For(0, oBatch, n =>
    //     {
    //         for (var y = 0; y < oHeight; ++y)
    //         for (var x = 0; x < oWidth; ++x)
    //         for (var k = 0; k < kKernelCount; ++k)
    //         {
    //             float v = bPtr[k];
    //             for (int dy = 0; dy < kKernelHeight; ++dy)
    //             {
    //                 int oy = y * stride[1] + dy - pad[1];
    //                 if (oy < 0) continue;
    //                 if (oy >= xHeight) continue;

    //                 for (int dx = 0; dx < kKernelWidth; ++dx)
    //                 {
    //                     int ox = x * stride[0] + dx - pad[0];
    //                     if (ox < 0) continue;
    //                     if (ox >= xWidth) continue;

    //                     float xv = xPtr[n * xnMult + oy * xyMult + ox * xxMult + k];
    //                     float kv = kPtr[dy * kyMult + dx * kxMult              + k];

    //                     v += xv * kv;
    //                 }
    //             }

    //             oPtr[n * onMult + y * oyMult + x * oxMult + k] = v;
    //         }
    //     });
    // }

    // private static unsafe void DepthwiseConv2DInnerLoop(int[] stride, int[] pad, int oBatch, int oHeight, int oWidth, int kKernelCount,
    //     float* bPtr, int kKernelHeight, int kKernelWidth, int xHeight, int xWidth, int xChannels, float* xPtr,
    //     int xnMult, int xyMult, int xxMult, float* kPtr, int kyMult, int kxMult, float* oPtr, int onMult,
    //     int oyMult, int oxMult)
    // {
    //     Parallel.For(0, oBatch, n =>
    //     {
    //         var ks = new float[kKernelCount];

    //         for (var y = 0; y < oHeight; ++y)
    //         for (var x = 0; x < oWidth; ++x)
    //         {
    //             for (int dy = 0; dy < kKernelHeight; ++dy)
    //             {
    //                 int oy = y * stride[1] + dy - pad[1];
    //                 if (oy < 0) continue;
    //                 if (oy >= xHeight) continue;

    //                 for (int dx = 0; dx < kKernelWidth; ++dx)
    //                 {
    //                     int ox = x * stride[0] + dx - pad[0];
    //                     if (ox < 0) continue;
    //                     if (ox >= xWidth) continue;

    //                     for (var k = 0; k < kKernelCount; ++k)
    //                     {
    //                         float xv = xPtr[n * xnMult + oy * xyMult + ox * xxMult + k];
    //                         float kv = kPtr[dy * kyMult + dx * kxMult              + k];

    //                         ks[k] += xv * kv;
    //                     }
    //                 }
    //             }

    //             for (var k = 0; k < kKernelCount; ++k)
    //             {
    //                 oPtr[n * onMult + y * oyMult + x * oxMult + k] = ks[k] + bPtr[k];
    //                 ks[k] = 0;
    //             }

    //         }
    //     });
    // }

    // private static unsafe void DepthwiseConv2DInnerLoop(int[] stride, int[] pad, int oBatch, int oHeight, int oWidth, int kKernelCount,
    //     float* bPtr, int kKernelHeight, int kKernelWidth, int xHeight, int xWidth, int xChannels, float* xPtr,
    //     int xnMult, int xyMult, int xxMult, float* kPtr, int kyMult, int kxMult, float* oPtr, int onMult,
    //     int oyMult, int oxMult)
    // {
    //     Parallel.For(0, oHeight, y =>
    //     {
    //         var ks = new float[kKernelCount];
    //         for (var n = 0; n < oBatch; ++n)
    //         for (var x = 0; x < oWidth; ++x)
    //         {
    //             for (int dy = 0; dy < kKernelHeight; ++dy)
    //             {
    //                 int oy = y * stride[1] + dy - pad[1];
    //                 if (oy < 0) continue;
    //                 if (oy >= xHeight) continue;

    //                 for (int dx = 0; dx < kKernelWidth; ++dx)
    //                 {
    //                     int ox = x * stride[0] + dx - pad[0];
    //                     if (ox < 0) continue;
    //                     if (ox >= xWidth) continue;

    //                     for (var k = 0; k < kKernelCount; ++k)
    //                     {
    //                         float xv = xPtr[n * xnMult + oy * xyMult + ox * xxMult + k];
    //                         float kv = kPtr[dy * kyMult + dx * kxMult              + k];

    //                         ks[k] += xv * kv;
    //                     }
    //                 }
    //             }

    //             for (var k = 0; k < kKernelCount; ++k)
    //             {
    //                 oPtr[n * onMult + y * oyMult + x * oxMult + k] = ks[k] + bPtr[k];
    //                 ks[k] = 0;
    //             }

    //         }
    //     });
    // }

    // private static unsafe void DepthwiseConv2DInnerLoop(int[] stride, int[] pad, int oBatch, int oHeight, int oWidth, int kKernelCount,
    //     float* bPtr, int kKernelHeight, int kKernelWidth, int xHeight, int xWidth, int xChannels, float* xPtr,
    //     int xnMult, int xyMult, int xxMult, float* kPtr, int kyMult, int kxMult, float* oPtr, int onMult,
    //     int oyMult, int oxMult)
    // {
    //     Parallel.For(0, oHeight, y =>
    //     {
    //         var ks = new float[kKernelCount];
    //         for (var n = 0; n < oBatch; ++n)
    //         for (var x = 0; x < oWidth; ++x)
    //         {
    //             for (int dy = 0; dy < kKernelHeight; ++dy)
    //             {

    //                 int oy = y * stride[1] + dy - pad[1];
    //                 if (oy < 0) continue;
    //                 if (oy >= xHeight) continue;

    //                 for (int dx = 0; dx < kKernelWidth; ++dx)
    //                 {
    //                     int ox = x * stride[0] + dx - pad[0];
    //                     if (ox < 0) continue;
    //                     if (ox >= xWidth) continue;

    //                     var k = 0;
    //                     for (; k < kKernelCount; k += 8)
    //                     {
    //                         var xIndex = n * xnMult + oy * xyMult + ox * xxMult + k;
    //                         var kIndex = dy * kyMult + dx * kxMult              + k;

    //                         float x0 = xPtr[xIndex + 0];
    //                         float k0 = kPtr[kIndex + 0];
    //                         float x1 = xPtr[xIndex + 1];
    //                         float k1 = kPtr[kIndex + 1];
    //                         float x2 = xPtr[xIndex + 2];
    //                         float k2 = kPtr[kIndex + 2];
    //                         float x3 = xPtr[xIndex + 3];
    //                         float k3 = kPtr[kIndex + 3];
    //                         float x4 = xPtr[xIndex + 4];
    //                         float k4 = kPtr[kIndex + 4];
    //                         float x5 = xPtr[xIndex + 5];
    //                         float k5 = kPtr[kIndex + 5];
    //                         float x6 = xPtr[xIndex + 6];
    //                         float k6 = kPtr[kIndex + 6];
    //                         float x7 = xPtr[xIndex + 7];
    //                         float k7 = kPtr[kIndex + 7];

    //                         ks[k + 0] += x0 * k0;
    //                         ks[k + 1] += x1 * k1;
    //                         ks[k + 2] += x2 * k2;
    //                         ks[k + 3] += x3 * k3;
    //                         ks[k + 4] += x4 * k4;
    //                         ks[k + 5] += x5 * k5;
    //                         ks[k + 6] += x6 * k6;
    //                         ks[k + 7] += x7 * k7;
    //                     }

    //                     for (; k < kKernelCount; k++)
    //                     {
    //                         var xIndex = n * xnMult + oy * xyMult + ox * xxMult + k;
    //                         var kIndex = dy * kyMult + dx * kxMult              + k;

    //                         float x0 = xPtr[xIndex];
    //                         float k0 = kPtr[kIndex];
    //                         ks[k] += x0 * k0;
    //                     }
    //                 }
    //             }

    //             var q = 0;
    //             for (; q < kKernelCount; q += 8)
    //             {
    //                 var oIndex = n * onMult + y * oyMult + x * oxMult + q;
    //                 oPtr[oIndex + 0] = ks[q + 0] + bPtr[q + 0]; ks[q + 0] = 0;
    //                 oPtr[oIndex + 1] = ks[q + 1] + bPtr[q + 1]; ks[q + 1] = 0;
    //                 oPtr[oIndex + 2] = ks[q + 2] + bPtr[q + 2]; ks[q + 2] = 0;
    //                 oPtr[oIndex + 3] = ks[q + 3] + bPtr[q + 3]; ks[q + 3] = 0;
    //                 oPtr[oIndex + 4] = ks[q + 4] + bPtr[q + 4]; ks[q + 4] = 0;
    //                 oPtr[oIndex + 5] = ks[q + 5] + bPtr[q + 5]; ks[q + 5] = 0;
    //                 oPtr[oIndex + 6] = ks[q + 6] + bPtr[q + 6]; ks[q + 6] = 0;
    //                 oPtr[oIndex + 7] = ks[q + 7] + bPtr[q + 7]; ks[q + 7] = 0;
    //             }
    //             for (; q < kKernelCount; q++)
    //             {
    //                 var oIndex = n * onMult + y * oyMult + x * oxMult + q;
    //                 oPtr[oIndex] = ks[q] + bPtr[q];
    //                 ks[q] = 0;
    //             }
    //         }
    //     });
    // }


    // private static unsafe void DepthwiseConv2DInnerLoop(int[] stride, int[] pad, int oBatch, int oHeight, int oWidth, int kKernelCount,
    //     float* bPtr, int kKernelHeight, int kKernelWidth, int xHeight, int xWidth, int xChannels, float* xPtr,
    //     int xnMult, int xyMult, int xxMult, float* kPtr, int kyMult, int kxMult, float* oPtr, int onMult,
    //     int oyMult, int oxMult)
    // {
    //     var unrollSize = 8;
    //     Parallel.For(0, oHeight, y =>
    //     {
    //         float* ks = (float*)UnsafeUtility.Malloc(kKernelCount * sizeof(float), 16 * sizeof(float), Allocator.TempJob);
    //         for (var n = 0; n < oBatch; ++n)
    //         for (var x = 0; x < oWidth; ++x)
    //         {
    //             for (int dy = 0; dy < kKernelHeight; ++dy)
    //             {
    //                 int oy = y * stride[1] + dy - pad[1];
    //                 if (oy < 0) continue;
    //                 if (oy >= xHeight) continue;

    //                 for (int dx = 0; dx < kKernelWidth; ++dx)
    //                 {
    //                     int ox = x * stride[0] + dx - pad[0];
    //                     if (ox < 0) continue;
    //                     if (ox >= xWidth) continue;

    //                     var k = 0;
    //                     for (; k < kKernelCount - (unrollSize - 1); k += unrollSize)
    //                     {
    //                         var xIndex = n * xnMult + oy * xyMult + ox * xxMult + k;
    //                         var kIndex = dy * kyMult + dx * kxMult              + k;

    //                         float x0 = xPtr[xIndex + 0], k0 = kPtr[kIndex + 0];
    //                         float x1 = xPtr[xIndex + 1], k1 = kPtr[kIndex + 1];
    //                         float x2 = xPtr[xIndex + 2], k2 = kPtr[kIndex + 2];
    //                         float x3 = xPtr[xIndex + 3], k3 = kPtr[kIndex + 3];
    //                         float x4 = xPtr[xIndex + 4], k4 = kPtr[kIndex + 4];
    //                         float x5 = xPtr[xIndex + 5], k5 = kPtr[kIndex + 5];
    //                         float x6 = xPtr[xIndex + 6], k6 = kPtr[kIndex + 6];
    //                         float x7 = xPtr[xIndex + 7], k7 = kPtr[kIndex + 7];

    //                         ks[k + 0] += x0 * k0;
    //                         ks[k + 1] += x1 * k1;
    //                         ks[k + 2] += x2 * k2;
    //                         ks[k + 3] += x3 * k3;
    //                         ks[k + 4] += x4 * k4;
    //                         ks[k + 5] += x5 * k5;
    //                         ks[k + 6] += x6 * k6;
    //                         ks[k + 7] += x7 * k7;
    //                     }

    //                     for (; k < kKernelCount; k++)
    //                     {
    //                         var xIndex = n * xnMult + oy * xyMult + ox * xxMult + k;
    //                         var kIndex = dy * kyMult + dx * kxMult              + k;

    //                         float x0 = xPtr[xIndex];
    //                         float k0 = kPtr[kIndex];
    //                         ks[k] += x0 * k0;
    //                     }
    //                 }
    //             }

    //             var q = 0;
    //             for (; q < kKernelCount - (unrollSize - 1); q += unrollSize)
    //             {
    //                 var oIndex = n * onMult + y * oyMult + x * oxMult + q;
    //                 oPtr[oIndex + 0] = ks[q + 0] + bPtr[q + 0]; ks[q + 0] = 0;
    //                 oPtr[oIndex + 1] = ks[q + 1] + bPtr[q + 1]; ks[q + 1] = 0;
    //                 oPtr[oIndex + 2] = ks[q + 2] + bPtr[q + 2]; ks[q + 2] = 0;
    //                 oPtr[oIndex + 3] = ks[q + 3] + bPtr[q + 3]; ks[q + 3] = 0;
    //                 oPtr[oIndex + 4] = ks[q + 4] + bPtr[q + 4]; ks[q + 4] = 0;
    //                 oPtr[oIndex + 5] = ks[q + 5] + bPtr[q + 5]; ks[q + 5] = 0;
    //                 oPtr[oIndex + 6] = ks[q + 6] + bPtr[q + 6]; ks[q + 6] = 0;
    //                 oPtr[oIndex + 7] = ks[q + 7] + bPtr[q + 7]; ks[q + 7] = 0;
    //             }
    //             for (; q < kKernelCount; q++)
    //             {
    //                 var oIndex = n * onMult + y * oyMult + x * oxMult + q;
    //                 oPtr[oIndex] = ks[q] + bPtr[q];
    //                 ks[q] = 0;
    //             }
    //         }
    //         UnsafeUtility.Free(ks, Allocator.TempJob);
    //     });
    // }



    private static unsafe void DepthwiseConv2DInnerLoop(int[] stride, int[] pad, int oBatch, int oHeight, int oWidth, int kKernelCount,
        float* bPtr, int kKernelHeight, int kKernelWidth, int xHeight, int xWidth, int xChannels, float* xPtr,
        int xnMult, int xyMult, int xxMult, float* kPtr, int kyMult, int kxMult, float* oPtr, int onMult,
        int oyMult, int oxMult)
    {
        var unrollSize = 8;
        var accumulatorMemSize = kKernelCount * sizeof(float);
        var accumulatorAlignmment = 16 * sizeof(float);

        Parallel.For(0, oHeight, y =>
        {
            float* outputAccumulators = (float*)UnsafeUtility.Malloc(accumulatorMemSize, accumulatorAlignmment, Allocator.TempJob);
            for (var n = 0; n < oBatch; ++n)
            for (var x = 0; x < oWidth; ++x)
            {
                // reset accumulators to 0
                UnsafeUtility.MemClear(outputAccumulators, accumulatorMemSize);

                for (int dy = 0; dy < kKernelHeight; ++dy)
                {
                    int oy = y * stride[1] + dy - pad[1];
                    if (oy < 0) continue;
                    if (oy >= xHeight) continue;

                    for (int dx = 0; dx < kKernelWidth; ++dx)
                    {
                        int ox = x * stride[0] + dx - pad[0];
                        if (ox < 0) continue;
                        if (ox >= xWidth) continue;

                        var k = 0;
                        var xIndex = n * xnMult + oy * xyMult + ox * xxMult;
                        var kIndex = dy * kyMult + dx * kxMult;
                        for (; k < kKernelCount - (unrollSize - 1); k += unrollSize)
                        {

                            float x0 = xPtr[xIndex + 0], k0 = kPtr[kIndex + 0];
                            float x1 = xPtr[xIndex + 1], k1 = kPtr[kIndex + 1];
                            float x2 = xPtr[xIndex + 2], k2 = kPtr[kIndex + 2];
                            float x3 = xPtr[xIndex + 3], k3 = kPtr[kIndex + 3];
                            float x4 = xPtr[xIndex + 4], k4 = kPtr[kIndex + 4];
                            float x5 = xPtr[xIndex + 5], k5 = kPtr[kIndex + 5];
                            float x6 = xPtr[xIndex + 6], k6 = kPtr[kIndex + 6];
                            float x7 = xPtr[xIndex + 7], k7 = kPtr[kIndex + 7];
                            xIndex += unrollSize;
                            kIndex += unrollSize;

                            outputAccumulators[k + 0] += x0 * k0;
                            outputAccumulators[k + 1] += x1 * k1;
                            outputAccumulators[k + 2] += x2 * k2;
                            outputAccumulators[k + 3] += x3 * k3;
                            outputAccumulators[k + 4] += x4 * k4;
                            outputAccumulators[k + 5] += x5 * k5;
                            outputAccumulators[k + 6] += x6 * k6;
                            outputAccumulators[k + 7] += x7 * k7;
                        }

                        for (; k < kKernelCount; k++)
                        {
                            float x0 = xPtr[xIndex++], k0 = kPtr[kIndex++];
                            outputAccumulators[k] += x0 * k0;
                        }
                    }
                }

                // write accumulators to memory
                var q = 0;
                var oIndex = n * onMult + y * oyMult + x * oxMult;
                for (; q < kKernelCount - (unrollSize - 1); q += unrollSize)
                {
                    oPtr[oIndex + 0] = outputAccumulators[q + 0] + bPtr[q + 0];
                    oPtr[oIndex + 1] = outputAccumulators[q + 1] + bPtr[q + 1];
                    oPtr[oIndex + 2] = outputAccumulators[q + 2] + bPtr[q + 2];
                    oPtr[oIndex + 3] = outputAccumulators[q + 3] + bPtr[q + 3];
                    oPtr[oIndex + 4] = outputAccumulators[q + 4] + bPtr[q + 4];
                    oPtr[oIndex + 5] = outputAccumulators[q + 5] + bPtr[q + 5];
                    oPtr[oIndex + 6] = outputAccumulators[q + 6] + bPtr[q + 6];
                    oPtr[oIndex + 7] = outputAccumulators[q + 7] + bPtr[q + 7];
                    oIndex += unrollSize;
                }
                for (; q < kKernelCount; q++)
                {
                    oPtr[oIndex++  ] = outputAccumulators[q    ] + bPtr[q    ];
                }
            }

            UnsafeUtility.Free(outputAccumulators, Allocator.TempJob);
        });
    }

    /// <inheritdoc/>
    protected override Tensor CopyAndReshape(Tensor X, TensorShape shape)
    {
        Assert.AreEqual(X.length, shape.length);
        var O = NewOutputTensor(X.dataType, shape);
        var pinO = Pin(O, uploadCache: false);
        BarracudaArray.Copy(Pin(X).array, Pin(X).offset, pinO.array, pinO.offset, X.length);
        return O;
    }

    private bool AreAnyTensorsHalf(Tensor[] tensors)
    {
        for (int i = 0; i != tensors.Length; ++i)
        {
            if (tensors[i].dataType == DataType.Half)
                return true;
        }
        return false;
    }

    private bool AreAnyTensorsHalf(Tensor tensor0, Tensor tensor1 = null, Tensor tensor2 = null, Tensor tensor3 = null)
    {
        if (tensor0.dataType == DataType.Half)
            return true;
        if (tensor1 != null && tensor1.dataType == DataType.Half)
            return true;
        if (tensor2 != null && tensor2.dataType == DataType.Half)
            return true;
        if (tensor3 != null && tensor3.dataType == DataType.Half)
            return true;

        return false;
    }

    /// <inheritdoc/>
    public override Tensor ScaleBias(Tensor X, Tensor S, Tensor B)
    {
        if (!X.shape.Is4D() || AreAnyTensorsHalf(X,S,B))
            return base.ScaleBias(X, S, B);

        Assert.AreEqual(X.channels, B.channels); Assert.AreEqual(X.channels, S.channels);
        Assert.AreEqual(B.length, B.channels); Assert.AreEqual(S.length, S.channels);

        // f(x) = x for x >= 0, f(x) = slope*x for x <= 0
        var O = NewTensorLike(X, AllocScope.LayerOutput);
        var end = X.length;
        const int unrollSize = 4;

        unsafe
        {
            float* xPtr = Pin(X).array.AddressAt(Pin(X).offset);
            float* oPtr = Pin(O, uploadCache: false).array.AddressAt(Pin(O, uploadCache: false).offset);
            float* sPtr = Pin(S).array.AddressAt(Pin(S).offset);
            float* bPtr = Pin(B).array.AddressAt(Pin(B).offset);
            {
                ScaleBiasInnerLoop(end, unrollSize, xPtr, X.length, oPtr, sPtr, S.length, bPtr, B.length);

                // Remainder
                for (int i = (end / unrollSize) * unrollSize; i < end; ++i)
                {
                    float v = xPtr[i];
                    float scale = sPtr[i % S.length];
                    float bias = bPtr[i % B.length];
                    v = v * scale + bias;
                    oPtr[i] = v;
                }
            }
        }

        return O;
    }

    private unsafe void ScaleBiasInnerLoop(int length, int unrollSize, float* xPtr, int xLen, float* oPtr, float* sPtr, int sLen, float* bPtr, int bLen)
    {
        Assert.AreEqual(unrollSize, 4);

        m_InnerLoop.SetState(unrollSize, oPtr, xPtr, xLen, sPtr, sLen, bPtr, bLen);

        Parallel_For(0L, length / unrollSize, m_InnerLoop.m_scaleBiasInnerLoopDelegate);
    }

    /// <inheritdoc/>
    public override Tensor Prepare(Tensor X)
    {
        Pin(X);
        return X;
    }

    /// <inheritdoc/>
    public override Tensor PrepareNoAlloc(Tensor X)
    {
        Pin(X, uploadCache: false);
        return X;
    }
}

    internal unsafe class InnerLoop
    {
        private int unrollSize;
        private float* oPtr;
        private float* xPtr;
        private int xLen;
        private float* sPtr;
        private int sLen;
        private float* bPtr;
        private int bLen;
        private float alpha;
        private float beta;
        private int prePadX;
        private int prePadY;

        private TensorShape oShape;
        private TensorShape xShape;
        private TensorShape sShape;
        private TensorShape bShape;

        public Action<long> m_tanhInnerLoopDelegate;
        public Action<long> m_expInnerLoopDelegate;
        public Action<long> m_sqrtInnerLoopDelegate;
        public Action<long> m_swishInnerLoopDelegate;
        public Action<long> m_softplusInnerLoopDelegate;
        public Action<long> m_sigmoidInnerLoopDelegate;
        public Action<long> m_hardsigmoidInnerLoopDelegate;
        public Action<long> m_negInnerLoopDelegate;
        public Action<long> m_eluInnerLoopDelegate;
        public Action<long> m_reluInnerLoopDelegate;
        public Action<long> m_relu6InnerLoopDelegate;
        public Action<long> m_leakyReluInnerLoopDelegate;
        public Action<long> m_preluInnerLoopDelegate;
        public Action<long> m_acosInnerLoopDelegate;
        public Action<long> m_acoshInnerLoopDelegate;
        public Action<long> m_asinInnerLoopDelegate;
        public Action<long> m_asinhInnerLoopDelegate;
        public Action<long> m_atanInnerLoopDelegate;
        public Action<long> m_atanhInnerLoopDelegate;
        public Action<long> m_cosInnerLoopDelegate;
        public Action<long> m_coshInnerLoopDelegate;
        public Action<long> m_sinInnerLoopDelegate;
        public Action<long> m_sinhInnerLoopDelegate;
        public Action<long> m_tanInnerLoopDelegate;
        public Action<long> m_erfInnerLoopDelegate;
        public Action<long> m_maxInnerLoopDelegate;
        public Action<long> m_minInnerLoopDelegate;
        public Action<long> m_divInnerLoopDelegate;
        public Action<long> m_mulInnerLoopDelegate;
        public Action<long> m_subInnerLoopDelegate;
        public Action<long> m_addInnerLoopDelegate;
        public Action<long> m_greaterInnerLoopDelegate;
        public Action<long> m_greaterEqualInnerLoopDelegate;
        public Action<long> m_lessInnerLoopDelegate;
        public Action<long> m_lessEqualInnerLoopDelegate;
        public Action<long> m_equalInnerLoopDelegate;
        public Action<long> m_logicalAndInnerLoopDelegate;
        public Action<long> m_logicalOrInnerLoopDelegate;
        public Action<long> m_logicalXorInnerLoopDelegate;
        public Action<long> m_logicalNotInnerLoopDelegate;
        public Action<long> m_signInnerLoopDelegate;
        public Action<long> m_whereInnerLoopDelegate;
        public Action<long> m_maxInnerLoopDelegateNoBroadcast;
        public Action<long> m_minInnerLoopDelegateNoBroadcast;
        public Action<long> m_divInnerLoopDelegateNoBroadcast;
        public Action<long> m_mulInnerLoopDelegateNoBroadcast;
        public Action<long> m_subInnerLoopDelegateNoBroadcast;
        public Action<long> m_addInnerLoopDelegateNoBroadcast;
        public Action<long> m_greaterInnerLoopDelegateNoBroadcast;
        public Action<long> m_greaterEqualInnerLoopDelegateNoBroadcast;
        public Action<long> m_lessInnerLoopDelegateNoBroadcast;
        public Action<long> m_lessEqualInnerLoopDelegateNoBroadcast;
        public Action<long> m_equalInnerLoopDelegateNoBroadcast;
        public Action<long> m_logicalAndInnerLoopDelegateNoBroadcast;
        public Action<long> m_logicalOrInnerLoopDelegateNoBroadcast;
        public Action<long> m_logicalXorInnerLoopDelegateNoBroadcast;
        public Action<long> m_whereInnerLoopDelegateNoBroadcast;
        public Action<long> m_scaleBiasInnerLoopDelegate;

        public Func<float,float,float> m_maxOpDelegate;
        public Func<float,float,float> m_minOpDelegate;
        public Func<float,float,float> m_divOpDelegate;
        public Func<float,float,float> m_mulOpDelegate;
        public Func<float,float,float> m_subOpDelegate;
        public Func<float,float,float> m_addOpDelegate;
        public Func<float,float,float> m_greaterOpDelegate;
        public Func<float,float,float> m_greaterEqualOpDelegate;
        public Func<float,float,float> m_lessOpDelegate;
        public Func<float,float,float> m_lessEqualOpDelegate;
        public Func<float,float,float> m_equalOpDelegate;
        public Func<float,float,float> m_logicalAndOpDelegate;
        public Func<float,float,float> m_logicalOrOpDelegate;
        public Func<float,float,float> m_logicalXorOpDelegate;
        public Func<float,float>       m_logicalNotOpDelegate;
        public Func<float, float>      m_signOpDelegate;

        public InnerLoop()
        {
            //Store delegates to avoid GC allocation because of repeated cast from functions to delegate at runtime
            m_tanhInnerLoopDelegate = TanhInnerLoop;
            m_expInnerLoopDelegate = ExpInnerLoop;
            m_sqrtInnerLoopDelegate = SqrtInnerLoop;
            m_swishInnerLoopDelegate = SwishInnerLoop;
            m_softplusInnerLoopDelegate = SoftplusInnerLoop;
            m_sigmoidInnerLoopDelegate = SigmoidInnerLoop;
            m_hardsigmoidInnerLoopDelegate = HardSigmoidInnerLoop;
            m_negInnerLoopDelegate = NegInnerLoop;
            m_eluInnerLoopDelegate = EluInnerLoop;
            m_reluInnerLoopDelegate = ReluInnerLoop;
            m_relu6InnerLoopDelegate = Relu6InnerLoop;
            m_leakyReluInnerLoopDelegate = LeakyReluInnerLoop;
            m_preluInnerLoopDelegate = PReluInnerLoop;
            m_acosInnerLoopDelegate = AcosInnerLoop;
            m_acoshInnerLoopDelegate = AcoshInnerLoop;
            m_asinInnerLoopDelegate = AsinInnerLoop;
            m_asinhInnerLoopDelegate = AsinhInnerLoop;
            m_atanInnerLoopDelegate = AtanInnerLoop;
            m_atanhInnerLoopDelegate = AtanhInnerLoop;
            m_cosInnerLoopDelegate = CosInnerLoop;
            m_coshInnerLoopDelegate = CoshInnerLoop;
            m_sinInnerLoopDelegate = SinInnerLoop;
            m_sinhInnerLoopDelegate = SinhInnerLoop;
            m_tanInnerLoopDelegate = TanInnerLoop;
            m_erfInnerLoopDelegate = ErfInnerLoop;
            m_maxInnerLoopDelegate = MaxInnerLoop;
            m_minInnerLoopDelegate = MinInnerLoop;
            m_divInnerLoopDelegate = DivInnerLoop;
            m_mulInnerLoopDelegate = MulInnerLoop;
            m_subInnerLoopDelegate = SubInnerLoop;
            m_addInnerLoopDelegate = AddInnerLoop;
            m_greaterInnerLoopDelegate = GreaterInnerLoop;
            m_greaterEqualInnerLoopDelegate = GreaterEqualInnerLoop;
            m_lessInnerLoopDelegate = LessInnerLoop;
            m_lessEqualInnerLoopDelegate = LessEqualInnerLoop;
            m_equalInnerLoopDelegate = EqualInnerLoop;
            m_logicalAndInnerLoopDelegate = LogicalAndInnerLoop;
            m_logicalOrInnerLoopDelegate = LogicalOrInnerLoop;
            m_logicalXorInnerLoopDelegate = LogicalXorInnerLoop;
            m_logicalNotInnerLoopDelegate = LogicalNotInnerLoop;
            m_signInnerLoopDelegate = SignInnerLoop;
            m_whereInnerLoopDelegate = WhereInnerLoop;
            m_maxInnerLoopDelegateNoBroadcast = MaxInnerLoopNoBroadcast;
            m_minInnerLoopDelegateNoBroadcast = MinInnerLoopNoBroadcast;
            m_divInnerLoopDelegateNoBroadcast = DivInnerLoopNoBroadcast;
            m_mulInnerLoopDelegateNoBroadcast = MulInnerLoopNoBroadcast;
            m_subInnerLoopDelegateNoBroadcast = SubInnerLoopNoBroadcast;
            m_addInnerLoopDelegateNoBroadcast = AddInnerLoopNoBroadcast;
            m_greaterInnerLoopDelegateNoBroadcast = GreaterInnerLoopNoBroadcast;
            m_greaterEqualInnerLoopDelegateNoBroadcast = GreaterEqualInnerLoopNoBroadcast;
            m_lessInnerLoopDelegateNoBroadcast = LessInnerLoopNoBroadcast;
            m_lessEqualInnerLoopDelegateNoBroadcast = LessEqualInnerLoopNoBroadcast;
            m_equalInnerLoopDelegateNoBroadcast = EqualInnerLoopNoBroadcast;
            m_logicalAndInnerLoopDelegateNoBroadcast = LogicalAndInnerLoopNoBroadcast;
            m_logicalOrInnerLoopDelegateNoBroadcast = LogicalOrInnerLoopNoBroadcast;
            m_logicalXorInnerLoopDelegateNoBroadcast = LogicalXorInnerLoopNoBroadcast;
            m_whereInnerLoopDelegateNoBroadcast = WhereInnerLoopNoBroadcast;
            m_scaleBiasInnerLoopDelegate = ScaleBiasInnerLoop;
            m_maxOpDelegate = Max;
            m_minOpDelegate = Min;
            m_divOpDelegate = Div;
            m_mulOpDelegate = Mul;
            m_subOpDelegate = Sub;
            m_addOpDelegate = Add;
            m_greaterOpDelegate = Greater;
            m_greaterEqualOpDelegate = GreaterEqual;
            m_lessOpDelegate = Less;
            m_lessEqualOpDelegate = LessEqual;
            m_equalOpDelegate = Equal;
            m_logicalAndOpDelegate = LogicalAnd;
            m_logicalOrOpDelegate = LogicalOr;
            m_logicalXorOpDelegate = LogicalXor;
            m_logicalNotOpDelegate = LogicalNot;
            m_signOpDelegate = Sign;
        }

        public void SetState(int unrollSize, float* oPtr, float* xPtr, float* sPtr, float* bPtr, TensorShape oShape, TensorShape xShape, TensorShape sShape, TensorShape bShape)
        {
            this.unrollSize = unrollSize;
            this.oPtr = oPtr;
            this.oShape = oShape;
            this.xPtr = xPtr;
            this.xShape = xShape;
            this.xLen = xShape.length;
            this.sPtr = sPtr;
            this.sShape = sShape;
            this.sLen = sShape.length;
            this.bPtr = bPtr;
            this.bShape = bShape;
            this.bLen = bShape.length;
        }

        public void SetState(int unrollSize, float* oPtr, float* xPtr, float* bPtr, TensorShape oShape, TensorShape xShape, TensorShape bShape)
        {
            this.unrollSize = unrollSize;
            this.oPtr = oPtr;
            this.oShape = oShape;
            this.xPtr = xPtr;
            this.xShape = xShape;
            this.xLen = xShape.length;
            this.bPtr = bPtr;
            this.bShape = bShape;
            this.bLen = bShape.length;
        }

        public void SetState(int unrollSize, float* oPtr, float* xPtr, int xLen, float* sPtr, int sLen, float* bPtr, int bLen)
        {
            this.unrollSize = unrollSize;
            this.oPtr = oPtr;
            this.xPtr = xPtr;
            this.xLen = xLen;
            this.sPtr = sPtr;
            this.sLen = sLen;
            this.bPtr = bPtr;
            this.bLen = bLen;
        }

        public void SetState(int unrollSize, float* oPtr, float* xPtr, int xLen, float* bPtr, int bLen)
        {
            this.unrollSize = unrollSize;
            this.oPtr = oPtr;
            this.xPtr = xPtr;
            this.xLen = xLen;
            this.bPtr = bPtr;
            this.bLen = bLen;
        }

        public void SetState(int unrollSize, float* xPtr, float* oPtr)
        {
            this.unrollSize = unrollSize;
            this.oPtr = oPtr;
            this.xPtr = xPtr;
        }

        public void SetState(int unrollSize, float* xPtr, float* oPtr, float* sPtr, float* bPtr)
        {
            this.unrollSize = unrollSize;
            this.oPtr = oPtr;
            this.xPtr = xPtr;
            this.sPtr = sPtr;
            this.bPtr = bPtr;
        }

        public void SetState(int unrollSize, float* xPtr, float* oPtr, float* bPtr)
        {
            this.unrollSize = unrollSize;
            this.oPtr = oPtr;
            this.xPtr = xPtr;
            this.bPtr = bPtr;
        }

        public void SetState(int unrollSize, float* xPtr, float* oPtr, float alpha)
        {
            this.unrollSize = unrollSize;
            this.oPtr = oPtr;
            this.xPtr = xPtr;
            this.alpha = alpha;
        }

        public void SetState(int unrollSize, float* xPtr, float* oPtr, float alpha, float beta)
        {
            this.unrollSize = unrollSize;
            this.oPtr = oPtr;
            this.xPtr = xPtr;
            this.alpha = alpha;
            this.beta = beta;
        }

        public void SetState(float* oPtr, float* xPtr, TensorShape oShape, TensorShape xShape, float constant, int prePadX, int prePadY)
        {
            this.oPtr = oPtr;
            this.xPtr = xPtr;
            this.oShape = oShape;
            this.xShape = xShape;
            this.alpha = constant;
            this.prePadX = prePadX;
            this.prePadY = prePadY;
        }

        private void NegInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = -v0;
            v1 = -v1;
            v2 = -v2;
            v3 = -v3;

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void ReluInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];
            float v4  = baseXPtr[4 ];
            float v5  = baseXPtr[5 ];
            float v6  = baseXPtr[6 ];
            float v7  = baseXPtr[7 ];
            float v8  = baseXPtr[8 ];
            float v9  = baseXPtr[9 ];
            float v10 = baseXPtr[10];
            float v11 = baseXPtr[11];
            float v12 = baseXPtr[12];
            float v13 = baseXPtr[13];
            float v14 = baseXPtr[14];
            float v15 = baseXPtr[15];
            float v16 = baseXPtr[16];
            float v17 = baseXPtr[17];
            float v18 = baseXPtr[18];
            float v19 = baseXPtr[19];
            float v20 = baseXPtr[20];
            float v21 = baseXPtr[21];
            float v22 = baseXPtr[22];
            float v23 = baseXPtr[23];
            float v24 = baseXPtr[24];
            float v25 = baseXPtr[25];
            float v26 = baseXPtr[26];
            float v27 = baseXPtr[27];
            float v28 = baseXPtr[28];
            float v29 = baseXPtr[29];
            float v30 = baseXPtr[30];
            float v31 = baseXPtr[31];
            float v32 = baseXPtr[32];
            float v33 = baseXPtr[33];
            float v34 = baseXPtr[34];
            float v35 = baseXPtr[35];
            float v36 = baseXPtr[36];
            float v37 = baseXPtr[37];
            float v38 = baseXPtr[38];
            float v39 = baseXPtr[39];
            float v40 = baseXPtr[40];
            float v41 = baseXPtr[41];
            float v42 = baseXPtr[42];
            float v43 = baseXPtr[43];
            float v44 = baseXPtr[44];
            float v45 = baseXPtr[45];
            float v46 = baseXPtr[46];
            float v47 = baseXPtr[47];
            float v48 = baseXPtr[48];
            float v49 = baseXPtr[49];
            float v50 = baseXPtr[50];
            float v51 = baseXPtr[51];
            float v52 = baseXPtr[52];
            float v53 = baseXPtr[53];
            float v54 = baseXPtr[54];
            float v55 = baseXPtr[55];
            float v56 = baseXPtr[56];
            float v57 = baseXPtr[57];
            float v58 = baseXPtr[58];
            float v59 = baseXPtr[59];
            float v60 = baseXPtr[60];
            float v61 = baseXPtr[61];
            float v62 = baseXPtr[62];
            float v63 = baseXPtr[63];

            v0  = 0.5f * (v0  + Math.Abs(v0 ));
            v1  = 0.5f * (v1  + Math.Abs(v1 ));
            v2  = 0.5f * (v2  + Math.Abs(v2 ));
            v3  = 0.5f * (v3  + Math.Abs(v3 ));
            v4  = 0.5f * (v4  + Math.Abs(v4 ));
            v5  = 0.5f * (v5  + Math.Abs(v5 ));
            v6  = 0.5f * (v6  + Math.Abs(v6 ));
            v7  = 0.5f * (v7  + Math.Abs(v7 ));
            v8  = 0.5f * (v8  + Math.Abs(v8 ));
            v9  = 0.5f * (v9  + Math.Abs(v9 ));
            v10 = 0.5f * (v10 + Math.Abs(v10));
            v11 = 0.5f * (v11 + Math.Abs(v11));
            v12 = 0.5f * (v12 + Math.Abs(v12));
            v13 = 0.5f * (v13 + Math.Abs(v13));
            v14 = 0.5f * (v14 + Math.Abs(v14));
            v15 = 0.5f * (v15 + Math.Abs(v15));
            v16 = 0.5f * (v16 + Math.Abs(v16));
            v17 = 0.5f * (v17 + Math.Abs(v17));
            v18 = 0.5f * (v18 + Math.Abs(v18));
            v19 = 0.5f * (v19 + Math.Abs(v19));
            v20 = 0.5f * (v20 + Math.Abs(v20));
            v21 = 0.5f * (v21 + Math.Abs(v21));
            v22 = 0.5f * (v22 + Math.Abs(v22));
            v23 = 0.5f * (v23 + Math.Abs(v23));
            v24 = 0.5f * (v24 + Math.Abs(v24));
            v25 = 0.5f * (v25 + Math.Abs(v25));
            v26 = 0.5f * (v26 + Math.Abs(v26));
            v27 = 0.5f * (v27 + Math.Abs(v27));
            v28 = 0.5f * (v28 + Math.Abs(v28));
            v29 = 0.5f * (v29 + Math.Abs(v29));
            v30 = 0.5f * (v30 + Math.Abs(v30));
            v31 = 0.5f * (v31 + Math.Abs(v31));
            v32 = 0.5f * (v32 + Math.Abs(v32));
            v33 = 0.5f * (v33 + Math.Abs(v33));
            v34 = 0.5f * (v34 + Math.Abs(v34));
            v35 = 0.5f * (v35 + Math.Abs(v35));
            v36 = 0.5f * (v36 + Math.Abs(v36));
            v37 = 0.5f * (v37 + Math.Abs(v37));
            v38 = 0.5f * (v38 + Math.Abs(v38));
            v39 = 0.5f * (v39 + Math.Abs(v39));
            v40 = 0.5f * (v40 + Math.Abs(v40));
            v41 = 0.5f * (v41 + Math.Abs(v41));
            v42 = 0.5f * (v42 + Math.Abs(v42));
            v43 = 0.5f * (v43 + Math.Abs(v43));
            v44 = 0.5f * (v44 + Math.Abs(v44));
            v45 = 0.5f * (v45 + Math.Abs(v45));
            v46 = 0.5f * (v46 + Math.Abs(v46));
            v47 = 0.5f * (v47 + Math.Abs(v47));
            v48 = 0.5f * (v48 + Math.Abs(v48));
            v49 = 0.5f * (v49 + Math.Abs(v49));
            v50 = 0.5f * (v50 + Math.Abs(v50));
            v51 = 0.5f * (v51 + Math.Abs(v51));
            v52 = 0.5f * (v52 + Math.Abs(v52));
            v53 = 0.5f * (v53 + Math.Abs(v53));
            v54 = 0.5f * (v54 + Math.Abs(v54));
            v55 = 0.5f * (v55 + Math.Abs(v55));
            v56 = 0.5f * (v56 + Math.Abs(v56));
            v57 = 0.5f * (v57 + Math.Abs(v57));
            v58 = 0.5f * (v58 + Math.Abs(v58));
            v59 = 0.5f * (v59 + Math.Abs(v59));
            v60 = 0.5f * (v60 + Math.Abs(v60));
            v61 = 0.5f * (v61 + Math.Abs(v61));
            v62 = 0.5f * (v62 + Math.Abs(v62));
            v63 = 0.5f * (v63 + Math.Abs(v63));

            baseOPtr[0 ] = v0 ;
            baseOPtr[1 ] = v1 ;
            baseOPtr[2 ] = v2 ;
            baseOPtr[3 ] = v3 ;
            baseOPtr[4 ] = v4 ;
            baseOPtr[5 ] = v5 ;
            baseOPtr[6 ] = v6 ;
            baseOPtr[7 ] = v7 ;
            baseOPtr[8 ] = v8 ;
            baseOPtr[9 ] = v9 ;
            baseOPtr[10] = v10;
            baseOPtr[11] = v11;
            baseOPtr[12] = v12;
            baseOPtr[13] = v13;
            baseOPtr[14] = v14;
            baseOPtr[15] = v15;
            baseOPtr[16] = v16;
            baseOPtr[17] = v17;
            baseOPtr[18] = v18;
            baseOPtr[19] = v19;
            baseOPtr[20] = v20;
            baseOPtr[21] = v21;
            baseOPtr[22] = v22;
            baseOPtr[23] = v23;
            baseOPtr[24] = v24;
            baseOPtr[25] = v25;
            baseOPtr[26] = v26;
            baseOPtr[27] = v27;
            baseOPtr[28] = v28;
            baseOPtr[29] = v29;
            baseOPtr[30] = v30;
            baseOPtr[31] = v31;
            baseOPtr[32] = v32;
            baseOPtr[33] = v33;
            baseOPtr[34] = v34;
            baseOPtr[35] = v35;
            baseOPtr[36] = v36;
            baseOPtr[37] = v37;
            baseOPtr[38] = v38;
            baseOPtr[39] = v39;
            baseOPtr[40] = v40;
            baseOPtr[41] = v41;
            baseOPtr[42] = v42;
            baseOPtr[43] = v43;
            baseOPtr[44] = v44;
            baseOPtr[45] = v45;
            baseOPtr[46] = v46;
            baseOPtr[47] = v47;
            baseOPtr[48] = v48;
            baseOPtr[49] = v49;
            baseOPtr[50] = v50;
            baseOPtr[51] = v51;
            baseOPtr[52] = v52;
            baseOPtr[53] = v53;
            baseOPtr[54] = v54;
            baseOPtr[55] = v55;
            baseOPtr[56] = v56;
            baseOPtr[57] = v57;
            baseOPtr[58] = v58;
            baseOPtr[59] = v59;
            baseOPtr[60] = v60;
            baseOPtr[61] = v61;
            baseOPtr[62] = v62;
            baseOPtr[63] = v63;
        }

        private void Relu6InnerLoop(long n)
        {
            // f(x) = min(max(x, 0), 6)
            // "Convolutional Deep Belief Networks on CIFAR-10", A Krizhevsky, 2010
            // http://www.cs.utoronto.ca/~kriz/conv-cifar10-aug2010.pdf

            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0  = baseXPtr[0 ];
            float v1  = baseXPtr[1 ];
            float v2  = baseXPtr[2 ];
            float v3  = baseXPtr[3 ];
            float v4  = baseXPtr[4 ];
            float v5  = baseXPtr[5 ];
            float v6  = baseXPtr[6 ];
            float v7  = baseXPtr[7 ];
            float v8  = baseXPtr[8 ];
            float v9  = baseXPtr[9 ];
            float v10 = baseXPtr[10];
            float v11 = baseXPtr[11];
            float v12 = baseXPtr[12];
            float v13 = baseXPtr[13];
            float v14 = baseXPtr[14];
            float v15 = baseXPtr[15];
            float v16 = baseXPtr[16];
            float v17 = baseXPtr[17];
            float v18 = baseXPtr[18];
            float v19 = baseXPtr[19];
            float v20 = baseXPtr[20];
            float v21 = baseXPtr[21];
            float v22 = baseXPtr[22];
            float v23 = baseXPtr[23];
            float v24 = baseXPtr[24];
            float v25 = baseXPtr[25];
            float v26 = baseXPtr[26];
            float v27 = baseXPtr[27];
            float v28 = baseXPtr[28];
            float v29 = baseXPtr[29];
            float v30 = baseXPtr[30];
            float v31 = baseXPtr[31];
            float v32 = baseXPtr[32];
            float v33 = baseXPtr[33];
            float v34 = baseXPtr[34];
            float v35 = baseXPtr[35];
            float v36 = baseXPtr[36];
            float v37 = baseXPtr[37];
            float v38 = baseXPtr[38];
            float v39 = baseXPtr[39];
            float v40 = baseXPtr[40];
            float v41 = baseXPtr[41];
            float v42 = baseXPtr[42];
            float v43 = baseXPtr[43];
            float v44 = baseXPtr[44];
            float v45 = baseXPtr[45];
            float v46 = baseXPtr[46];
            float v47 = baseXPtr[47];
            float v48 = baseXPtr[48];
            float v49 = baseXPtr[49];
            float v50 = baseXPtr[50];
            float v51 = baseXPtr[51];
            float v52 = baseXPtr[52];
            float v53 = baseXPtr[53];
            float v54 = baseXPtr[54];
            float v55 = baseXPtr[55];
            float v56 = baseXPtr[56];
            float v57 = baseXPtr[57];
            float v58 = baseXPtr[58];
            float v59 = baseXPtr[59];
            float v60 = baseXPtr[60];
            float v61 = baseXPtr[61];
            float v62 = baseXPtr[62];
            float v63 = baseXPtr[63];

            v0  = 0.5f * (-Math.Abs(v0  - 6f) + Math.Abs(v0)  + 6f);
            v1  = 0.5f * (-Math.Abs(v1  - 6f) + Math.Abs(v1)  + 6f);
            v2  = 0.5f * (-Math.Abs(v2  - 6f) + Math.Abs(v2)  + 6f);
            v3  = 0.5f * (-Math.Abs(v3  - 6f) + Math.Abs(v3)  + 6f);
            v4  = 0.5f * (-Math.Abs(v4  - 6f) + Math.Abs(v4)  + 6f);
            v5  = 0.5f * (-Math.Abs(v5  - 6f) + Math.Abs(v5)  + 6f);
            v6  = 0.5f * (-Math.Abs(v6  - 6f) + Math.Abs(v6)  + 6f);
            v7  = 0.5f * (-Math.Abs(v7  - 6f) + Math.Abs(v7)  + 6f);
            v8  = 0.5f * (-Math.Abs(v8  - 6f) + Math.Abs(v8)  + 6f);
            v9  = 0.5f * (-Math.Abs(v9  - 6f) + Math.Abs(v9)  + 6f);
            v10 = 0.5f * (-Math.Abs(v10 - 6f) + Math.Abs(v10) + 6f);
            v11 = 0.5f * (-Math.Abs(v11 - 6f) + Math.Abs(v11) + 6f);
            v12 = 0.5f * (-Math.Abs(v12 - 6f) + Math.Abs(v12) + 6f);
            v13 = 0.5f * (-Math.Abs(v13 - 6f) + Math.Abs(v13) + 6f);
            v14 = 0.5f * (-Math.Abs(v14 - 6f) + Math.Abs(v14) + 6f);
            v15 = 0.5f * (-Math.Abs(v15 - 6f) + Math.Abs(v15) + 6f);
            v16 = 0.5f * (-Math.Abs(v16 - 6f) + Math.Abs(v16) + 6f);
            v17 = 0.5f * (-Math.Abs(v17 - 6f) + Math.Abs(v17) + 6f);
            v18 = 0.5f * (-Math.Abs(v18 - 6f) + Math.Abs(v18) + 6f);
            v19 = 0.5f * (-Math.Abs(v19 - 6f) + Math.Abs(v19) + 6f);
            v20 = 0.5f * (-Math.Abs(v20 - 6f) + Math.Abs(v20) + 6f);
            v21 = 0.5f * (-Math.Abs(v21 - 6f) + Math.Abs(v21) + 6f);
            v22 = 0.5f * (-Math.Abs(v22 - 6f) + Math.Abs(v22) + 6f);
            v23 = 0.5f * (-Math.Abs(v23 - 6f) + Math.Abs(v23) + 6f);
            v24 = 0.5f * (-Math.Abs(v24 - 6f) + Math.Abs(v24) + 6f);
            v25 = 0.5f * (-Math.Abs(v25 - 6f) + Math.Abs(v25) + 6f);
            v26 = 0.5f * (-Math.Abs(v26 - 6f) + Math.Abs(v26) + 6f);
            v27 = 0.5f * (-Math.Abs(v27 - 6f) + Math.Abs(v27) + 6f);
            v28 = 0.5f * (-Math.Abs(v28 - 6f) + Math.Abs(v28) + 6f);
            v29 = 0.5f * (-Math.Abs(v29 - 6f) + Math.Abs(v29) + 6f);
            v30 = 0.5f * (-Math.Abs(v30 - 6f) + Math.Abs(v30) + 6f);
            v31 = 0.5f * (-Math.Abs(v31 - 6f) + Math.Abs(v31) + 6f);
            v32 = 0.5f * (-Math.Abs(v32 - 6f) + Math.Abs(v32) + 6f);
            v33 = 0.5f * (-Math.Abs(v33 - 6f) + Math.Abs(v33) + 6f);
            v34 = 0.5f * (-Math.Abs(v34 - 6f) + Math.Abs(v34) + 6f);
            v35 = 0.5f * (-Math.Abs(v35 - 6f) + Math.Abs(v35) + 6f);
            v36 = 0.5f * (-Math.Abs(v36 - 6f) + Math.Abs(v36) + 6f);
            v37 = 0.5f * (-Math.Abs(v37 - 6f) + Math.Abs(v37) + 6f);
            v38 = 0.5f * (-Math.Abs(v38 - 6f) + Math.Abs(v38) + 6f);
            v39 = 0.5f * (-Math.Abs(v39 - 6f) + Math.Abs(v39) + 6f);
            v40 = 0.5f * (-Math.Abs(v40 - 6f) + Math.Abs(v40) + 6f);
            v41 = 0.5f * (-Math.Abs(v41 - 6f) + Math.Abs(v41) + 6f);
            v42 = 0.5f * (-Math.Abs(v42 - 6f) + Math.Abs(v42) + 6f);
            v43 = 0.5f * (-Math.Abs(v43 - 6f) + Math.Abs(v43) + 6f);
            v44 = 0.5f * (-Math.Abs(v44 - 6f) + Math.Abs(v44) + 6f);
            v45 = 0.5f * (-Math.Abs(v45 - 6f) + Math.Abs(v45) + 6f);
            v46 = 0.5f * (-Math.Abs(v46 - 6f) + Math.Abs(v46) + 6f);
            v47 = 0.5f * (-Math.Abs(v47 - 6f) + Math.Abs(v47) + 6f);
            v48 = 0.5f * (-Math.Abs(v48 - 6f) + Math.Abs(v48) + 6f);
            v49 = 0.5f * (-Math.Abs(v49 - 6f) + Math.Abs(v49) + 6f);
            v50 = 0.5f * (-Math.Abs(v50 - 6f) + Math.Abs(v50) + 6f);
            v51 = 0.5f * (-Math.Abs(v51 - 6f) + Math.Abs(v51) + 6f);
            v52 = 0.5f * (-Math.Abs(v52 - 6f) + Math.Abs(v52) + 6f);
            v53 = 0.5f * (-Math.Abs(v53 - 6f) + Math.Abs(v53) + 6f);
            v54 = 0.5f * (-Math.Abs(v54 - 6f) + Math.Abs(v54) + 6f);
            v55 = 0.5f * (-Math.Abs(v55 - 6f) + Math.Abs(v55) + 6f);
            v56 = 0.5f * (-Math.Abs(v56 - 6f) + Math.Abs(v56) + 6f);
            v57 = 0.5f * (-Math.Abs(v57 - 6f) + Math.Abs(v57) + 6f);
            v58 = 0.5f * (-Math.Abs(v58 - 6f) + Math.Abs(v58) + 6f);
            v59 = 0.5f * (-Math.Abs(v59 - 6f) + Math.Abs(v59) + 6f);
            v60 = 0.5f * (-Math.Abs(v60 - 6f) + Math.Abs(v60) + 6f);
            v61 = 0.5f * (-Math.Abs(v61 - 6f) + Math.Abs(v61) + 6f);
            v62 = 0.5f * (-Math.Abs(v62 - 6f) + Math.Abs(v62) + 6f);
            v63 = 0.5f * (-Math.Abs(v63 - 6f) + Math.Abs(v63) + 6f);

            baseOPtr[0 ] = v0 ;
            baseOPtr[1 ] = v1 ;
            baseOPtr[2 ] = v2 ;
            baseOPtr[3 ] = v3 ;
            baseOPtr[4 ] = v4 ;
            baseOPtr[5 ] = v5 ;
            baseOPtr[6 ] = v6 ;
            baseOPtr[7 ] = v7 ;
            baseOPtr[8 ] = v8 ;
            baseOPtr[9 ] = v9 ;
            baseOPtr[10] = v10;
            baseOPtr[11] = v11;
            baseOPtr[12] = v12;
            baseOPtr[13] = v13;
            baseOPtr[14] = v14;
            baseOPtr[15] = v15;
            baseOPtr[16] = v16;
            baseOPtr[17] = v17;
            baseOPtr[18] = v18;
            baseOPtr[19] = v19;
            baseOPtr[20] = v20;
            baseOPtr[21] = v21;
            baseOPtr[22] = v22;
            baseOPtr[23] = v23;
            baseOPtr[24] = v24;
            baseOPtr[25] = v25;
            baseOPtr[26] = v26;
            baseOPtr[27] = v27;
            baseOPtr[28] = v28;
            baseOPtr[29] = v29;
            baseOPtr[30] = v30;
            baseOPtr[31] = v31;
            baseOPtr[32] = v32;
            baseOPtr[33] = v33;
            baseOPtr[34] = v34;
            baseOPtr[35] = v35;
            baseOPtr[36] = v36;
            baseOPtr[37] = v37;
            baseOPtr[38] = v38;
            baseOPtr[39] = v39;
            baseOPtr[40] = v40;
            baseOPtr[41] = v41;
            baseOPtr[42] = v42;
            baseOPtr[43] = v43;
            baseOPtr[44] = v44;
            baseOPtr[45] = v45;
            baseOPtr[46] = v46;
            baseOPtr[47] = v47;
            baseOPtr[48] = v48;
            baseOPtr[49] = v49;
            baseOPtr[50] = v50;
            baseOPtr[51] = v51;
            baseOPtr[52] = v52;
            baseOPtr[53] = v53;
            baseOPtr[54] = v54;
            baseOPtr[55] = v55;
            baseOPtr[56] = v56;
            baseOPtr[57] = v57;
            baseOPtr[58] = v58;
            baseOPtr[59] = v59;
            baseOPtr[60] = v60;
            baseOPtr[61] = v61;
            baseOPtr[62] = v62;
            baseOPtr[63] = v63;
        }

        private void LeakyReluInnerLoop(long n)
        {
            // f(x) = alpha * x for x < 0, f(x) = x for x >= 0.
            // "Rectifier Nonlinearities Improve Neural Network Acoustic Models". AL Maas, 2013
            // http://web.stanford.edu/~awni/papers/relu_hybrid_icml2013_final.pdf

            // from Theano impl
            // https://github.com/Theano/theano/blob/d395439aec5a6ddde8ef5c266fd976412a5c5695/theano/tensor/nnet/nnet.py#L2209-L2251
            float f1 = 0.5f * (1f + alpha);
            float f2 = 0.5f * (1f - alpha);

            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0  = baseXPtr[0 ];
            float v1  = baseXPtr[1 ];
            float v2  = baseXPtr[2 ];
            float v3  = baseXPtr[3 ];
            float v4  = baseXPtr[4 ];
            float v5  = baseXPtr[5 ];
            float v6  = baseXPtr[6 ];
            float v7  = baseXPtr[7 ];
            float v8  = baseXPtr[8 ];
            float v9  = baseXPtr[9 ];
            float v10 = baseXPtr[10];
            float v11 = baseXPtr[11];
            float v12 = baseXPtr[12];
            float v13 = baseXPtr[13];
            float v14 = baseXPtr[14];
            float v15 = baseXPtr[15];
            float v16 = baseXPtr[16];
            float v17 = baseXPtr[17];
            float v18 = baseXPtr[18];
            float v19 = baseXPtr[19];
            float v20 = baseXPtr[20];
            float v21 = baseXPtr[21];
            float v22 = baseXPtr[22];
            float v23 = baseXPtr[23];
            float v24 = baseXPtr[24];
            float v25 = baseXPtr[25];
            float v26 = baseXPtr[26];
            float v27 = baseXPtr[27];
            float v28 = baseXPtr[28];
            float v29 = baseXPtr[29];
            float v30 = baseXPtr[30];
            float v31 = baseXPtr[31];
            float v32 = baseXPtr[32];
            float v33 = baseXPtr[33];
            float v34 = baseXPtr[34];
            float v35 = baseXPtr[35];
            float v36 = baseXPtr[36];
            float v37 = baseXPtr[37];
            float v38 = baseXPtr[38];
            float v39 = baseXPtr[39];
            float v40 = baseXPtr[40];
            float v41 = baseXPtr[41];
            float v42 = baseXPtr[42];
            float v43 = baseXPtr[43];
            float v44 = baseXPtr[44];
            float v45 = baseXPtr[45];
            float v46 = baseXPtr[46];
            float v47 = baseXPtr[47];
            float v48 = baseXPtr[48];
            float v49 = baseXPtr[49];
            float v50 = baseXPtr[50];
            float v51 = baseXPtr[51];
            float v52 = baseXPtr[52];
            float v53 = baseXPtr[53];
            float v54 = baseXPtr[54];
            float v55 = baseXPtr[55];
            float v56 = baseXPtr[56];
            float v57 = baseXPtr[57];
            float v58 = baseXPtr[58];
            float v59 = baseXPtr[59];
            float v60 = baseXPtr[60];
            float v61 = baseXPtr[61];
            float v62 = baseXPtr[62];
            float v63 = baseXPtr[63];

            v0  = f1 * v0  + f2 * Math.Abs(v0) ;
            v1  = f1 * v1  + f2 * Math.Abs(v1) ;
            v2  = f1 * v2  + f2 * Math.Abs(v2) ;
            v3  = f1 * v3  + f2 * Math.Abs(v3) ;
            v4  = f1 * v4  + f2 * Math.Abs(v4) ;
            v5  = f1 * v5  + f2 * Math.Abs(v5) ;
            v6  = f1 * v6  + f2 * Math.Abs(v6) ;
            v7  = f1 * v7  + f2 * Math.Abs(v7) ;
            v8  = f1 * v8  + f2 * Math.Abs(v8) ;
            v9  = f1 * v9  + f2 * Math.Abs(v9) ;
            v10 = f1 * v10 + f2 * Math.Abs(v10);
            v11 = f1 * v11 + f2 * Math.Abs(v11);
            v12 = f1 * v12 + f2 * Math.Abs(v12);
            v13 = f1 * v13 + f2 * Math.Abs(v13);
            v14 = f1 * v14 + f2 * Math.Abs(v14);
            v15 = f1 * v15 + f2 * Math.Abs(v15);
            v16 = f1 * v16 + f2 * Math.Abs(v16);
            v17 = f1 * v17 + f2 * Math.Abs(v17);
            v18 = f1 * v18 + f2 * Math.Abs(v18);
            v19 = f1 * v19 + f2 * Math.Abs(v19);
            v20 = f1 * v20 + f2 * Math.Abs(v20);
            v21 = f1 * v21 + f2 * Math.Abs(v21);
            v22 = f1 * v22 + f2 * Math.Abs(v22);
            v23 = f1 * v23 + f2 * Math.Abs(v23);
            v24 = f1 * v24 + f2 * Math.Abs(v24);
            v25 = f1 * v25 + f2 * Math.Abs(v25);
            v26 = f1 * v26 + f2 * Math.Abs(v26);
            v27 = f1 * v27 + f2 * Math.Abs(v27);
            v28 = f1 * v28 + f2 * Math.Abs(v28);
            v29 = f1 * v29 + f2 * Math.Abs(v29);
            v30 = f1 * v30 + f2 * Math.Abs(v30);
            v31 = f1 * v31 + f2 * Math.Abs(v31);
            v32 = f1 * v32 + f2 * Math.Abs(v32);
            v33 = f1 * v33 + f2 * Math.Abs(v33);
            v34 = f1 * v34 + f2 * Math.Abs(v34);
            v35 = f1 * v35 + f2 * Math.Abs(v35);
            v36 = f1 * v36 + f2 * Math.Abs(v36);
            v37 = f1 * v37 + f2 * Math.Abs(v37);
            v38 = f1 * v38 + f2 * Math.Abs(v38);
            v39 = f1 * v39 + f2 * Math.Abs(v39);
            v40 = f1 * v40 + f2 * Math.Abs(v40);
            v41 = f1 * v41 + f2 * Math.Abs(v41);
            v42 = f1 * v42 + f2 * Math.Abs(v42);
            v43 = f1 * v43 + f2 * Math.Abs(v43);
            v44 = f1 * v44 + f2 * Math.Abs(v44);
            v45 = f1 * v45 + f2 * Math.Abs(v45);
            v46 = f1 * v46 + f2 * Math.Abs(v46);
            v47 = f1 * v47 + f2 * Math.Abs(v47);
            v48 = f1 * v48 + f2 * Math.Abs(v48);
            v49 = f1 * v49 + f2 * Math.Abs(v49);
            v50 = f1 * v50 + f2 * Math.Abs(v50);
            v51 = f1 * v51 + f2 * Math.Abs(v51);
            v52 = f1 * v52 + f2 * Math.Abs(v52);
            v53 = f1 * v53 + f2 * Math.Abs(v53);
            v54 = f1 * v54 + f2 * Math.Abs(v54);
            v55 = f1 * v55 + f2 * Math.Abs(v55);
            v56 = f1 * v56 + f2 * Math.Abs(v56);
            v57 = f1 * v57 + f2 * Math.Abs(v57);
            v58 = f1 * v58 + f2 * Math.Abs(v58);
            v59 = f1 * v59 + f2 * Math.Abs(v59);
            v60 = f1 * v60 + f2 * Math.Abs(v60);
            v61 = f1 * v61 + f2 * Math.Abs(v61);
            v62 = f1 * v62 + f2 * Math.Abs(v62);
            v63 = f1 * v63 + f2 * Math.Abs(v63);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
            baseOPtr[4 ] = v4 ;
            baseOPtr[5 ] = v5 ;
            baseOPtr[6 ] = v6 ;
            baseOPtr[7 ] = v7 ;
            baseOPtr[8 ] = v8 ;
            baseOPtr[9 ] = v9 ;
            baseOPtr[10] = v10;
            baseOPtr[11] = v11;
            baseOPtr[12] = v12;
            baseOPtr[13] = v13;
            baseOPtr[14] = v14;
            baseOPtr[15] = v15;
            baseOPtr[16] = v16;
            baseOPtr[17] = v17;
            baseOPtr[18] = v18;
            baseOPtr[19] = v19;
            baseOPtr[20] = v20;
            baseOPtr[21] = v21;
            baseOPtr[22] = v22;
            baseOPtr[23] = v23;
            baseOPtr[24] = v24;
            baseOPtr[25] = v25;
            baseOPtr[26] = v26;
            baseOPtr[27] = v27;
            baseOPtr[28] = v28;
            baseOPtr[29] = v29;
            baseOPtr[30] = v30;
            baseOPtr[31] = v31;
            baseOPtr[32] = v32;
            baseOPtr[33] = v33;
            baseOPtr[34] = v34;
            baseOPtr[35] = v35;
            baseOPtr[36] = v36;
            baseOPtr[37] = v37;
            baseOPtr[38] = v38;
            baseOPtr[39] = v39;
            baseOPtr[40] = v40;
            baseOPtr[41] = v41;
            baseOPtr[42] = v42;
            baseOPtr[43] = v43;
            baseOPtr[44] = v44;
            baseOPtr[45] = v45;
            baseOPtr[46] = v46;
            baseOPtr[47] = v47;
            baseOPtr[48] = v48;
            baseOPtr[49] = v49;
            baseOPtr[50] = v50;
            baseOPtr[51] = v51;
            baseOPtr[52] = v52;
            baseOPtr[53] = v53;
            baseOPtr[54] = v54;
            baseOPtr[55] = v55;
            baseOPtr[56] = v56;
            baseOPtr[57] = v57;
            baseOPtr[58] = v58;
            baseOPtr[59] = v59;
            baseOPtr[60] = v60;
            baseOPtr[61] = v61;
            baseOPtr[62] = v62;
            baseOPtr[63] = v63;
        }

        private void EluInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            if (v0 <= 0)
                v0 = alpha * (Mathf.Exp(v0) - 1f);
            if (v1 <= 0)
                v1 = alpha * (Mathf.Exp(v1) - 1f);
            if (v2 <= 0)
                v2 = alpha * (Mathf.Exp(v2) - 1f);
            if (v3 <= 0)
                v3 = alpha * (Mathf.Exp(v3) - 1f);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void PReluInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float* baseBPtr = bPtr + (n * unrollSize) % bLen;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            float s0 = baseBPtr[0 % bLen];
            float s1 = baseBPtr[1 % bLen];
            float s2 = baseBPtr[2 % bLen];
            float s3 = baseBPtr[3 % bLen];

            if (v0 <= 0)
                v0 = s0 * v0;
            if (v1 <= 0)
                v1 = s1 * v1;
            if (v2 <= 0)
                v2 = s2 * v2;
            if (v3 <= 0)
                v3 = s3 * v3;

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void SoftplusInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Log(Mathf.Exp(v0) + 1f);
            v1 = Mathf.Log(Mathf.Exp(v1) + 1f);
            v2 = Mathf.Log(Mathf.Exp(v2) + 1f);
            v3 = Mathf.Log(Mathf.Exp(v3) + 1f);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void SigmoidInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = 1f / (1f + Mathf.Exp(-v0));
            v1 = 1f / (1f + Mathf.Exp(-v1));
            v2 = 1f / (1f + Mathf.Exp(-v2));
            v3 = 1f / (1f + Mathf.Exp(-v3));

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void HardSigmoidInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Max(0.0f, Mathf.Min(1.0f, alpha * v0 + beta));
            v1 = Mathf.Max(0.0f, Mathf.Min(1.0f, alpha * v1 + beta));
            v2 = Mathf.Max(0.0f, Mathf.Min(1.0f, alpha * v2 + beta));
            v3 = Mathf.Max(0.0f, Mathf.Min(1.0f, alpha * v3 + beta));

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void SwishInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = v0 / (1f + Mathf.Exp(-v0));
            v1 = v1 / (1f + Mathf.Exp(-v1));
            v2 = v2 / (1f + Mathf.Exp(-v2));
            v3 = v3 / (1f + Mathf.Exp(-v3));

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void ExpInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Exp(v0);
            v1 = Mathf.Exp(v1);
            v2 = Mathf.Exp(v2);
            v3 = Mathf.Exp(v3);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void SqrtInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Sqrt(v0);
            v1 = Mathf.Sqrt(v1);
            v2 = Mathf.Sqrt(v2);
            v3 = Mathf.Sqrt(v3);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void TanhInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = MathfEx.Tanh(v0);
            v1 = MathfEx.Tanh(v1);
            v2 = MathfEx.Tanh(v2);
            v3 = MathfEx.Tanh(v3);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void AcosInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Acos(v0);
            v1 = Mathf.Acos(v1);
            v2 = Mathf.Acos(v2);
            v3 = Mathf.Acos(v3);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void AcoshInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Log(v0 + Mathf.Sqrt(v0 * v0 - 1.0f));
            v1 = Mathf.Log(v1 + Mathf.Sqrt(v1 * v1 - 1.0f));
            v2 = Mathf.Log(v2 + Mathf.Sqrt(v2 * v2 - 1.0f));
            v3 = Mathf.Log(v3 + Mathf.Sqrt(v3 * v3 - 1.0f));

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void AsinInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Asin(v0);
            v1 = Mathf.Asin(v1);
            v2 = Mathf.Asin(v2);
            v3 = Mathf.Asin(v3);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void AsinhInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Log(v0 + Mathf.Sqrt(v0 * v0 + 1.0f));
            v1 = Mathf.Log(v1 + Mathf.Sqrt(v1 * v1 + 1.0f));
            v2 = Mathf.Log(v2 + Mathf.Sqrt(v2 * v2 + 1.0f));
            v3 = Mathf.Log(v3 + Mathf.Sqrt(v3 * v3 + 1.0f));

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void AtanInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Atan(v0);
            v1 = Mathf.Atan(v1);
            v2 = Mathf.Atan(v2);
            v3 = Mathf.Atan(v3);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void AtanhInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = 0.5f * Mathf.Log((1.0f + v0) / (1.0f - v0));
            v1 = 0.5f * Mathf.Log((1.0f + v1) / (1.0f - v1));
            v2 = 0.5f * Mathf.Log((1.0f + v2) / (1.0f - v2));
            v3 = 0.5f * Mathf.Log((1.0f + v3) / (1.0f - v3));

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void CosInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Cos(v0);
            v1 = Mathf.Cos(v1);
            v2 = Mathf.Cos(v2);
            v3 = Mathf.Cos(v3);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void CoshInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = 0.5f * (Mathf.Exp(v0) + Mathf.Exp(-v0));
            v1 = 0.5f * (Mathf.Exp(v1) + Mathf.Exp(-v1));
            v2 = 0.5f * (Mathf.Exp(v2) + Mathf.Exp(-v2));
            v3 = 0.5f * (Mathf.Exp(v3) + Mathf.Exp(-v3));

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void SinInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Sin(v0);
            v1 = Mathf.Sin(v1);
            v2 = Mathf.Sin(v2);
            v3 = Mathf.Sin(v3);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void SinhInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = 0.5f * (Mathf.Exp(v0) - Mathf.Exp(-v0));
            v1 = 0.5f * (Mathf.Exp(v1) - Mathf.Exp(-v1));
            v2 = 0.5f * (Mathf.Exp(v2) - Mathf.Exp(-v2));
            v3 = 0.5f * (Mathf.Exp(v3) - Mathf.Exp(-v3));

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void TanInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            v0 = Mathf.Tan(v0);
            v1 = Mathf.Tan(v1);
            v2 = Mathf.Tan(v2);
            v3 = Mathf.Tan(v3);

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void ErfInnerLoop(long n)
        {
            float* baseXPtr = xPtr + n * unrollSize;
            float* baseOPtr = oPtr + n * unrollSize;
            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            // Abramowitz/Stegun approximations
            // erf(x) = -erf(-x)
            float x0 = Mathf.Abs(v0);
            float x1 = Mathf.Abs(v1);
            float x2 = Mathf.Abs(v2);
            float x3 = Mathf.Abs(v3);

            float p = 0.3275911f;
            float a1 = 0.254829592f; float a2 = -0.284496736f; float a3 = 1.421413741f;
            float a4 = -1.453152027f; float a5 = 1.061405429f;

            float t0 = 1.0f / (1.0f + p * x0);
            float t1 = 1.0f / (1.0f + p * x1);
            float t2 = 1.0f / (1.0f + p * x2);
            float t3 = 1.0f / (1.0f + p * x3);

            v0 = Mathf.Sign(v0) * (1 - (a1 * (t0) + a2 * (t0*t0) + a3 * (t0*t0*t0) + a4 * (t0*t0*t0*t0) + a5 * (t0*t0*t0*t0*t0)) * Mathf.Exp(-x0 * x0));
            v1 = Mathf.Sign(v1) * (1 - (a1 * (t1) + a2 * (t1*t1) + a3 * (t1*t1*t1) + a4 * (t1*t1*t1*t1) + a5 * (t1*t1*t1*t1*t1)) * Mathf.Exp(-x1 * x1));
            v2 = Mathf.Sign(v2) * (1 - (a1 * (t2) + a2 * (t2*t2) + a3 * (t2*t2*t2) + a4 * (t2*t2*t2*t2) + a5 * (t2*t2*t2*t2*t2)) * Mathf.Exp(-x2 * x2));
            v3 = Mathf.Sign(v3) * (1 - (a1 * (t3) + a2 * (t3*t3) + a3 * (t3*t3*t3) + a4 * (t3*t3*t3*t3) + a5 * (t3*t3*t3*t3*t3)) * Mathf.Exp(-x3 * x3));

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private void AddInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)] + bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)];
            oPtr[i + 1] = xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)] + bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)];
            oPtr[i + 2] = xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)] + bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)];
            oPtr[i + 3] = xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)] + bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)];
        }

        private void SubInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)] - bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)];
            oPtr[i + 1] = xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)] - bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)];
            oPtr[i + 2] = xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)] - bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)];
            oPtr[i + 3] = xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)] - bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)];
        }

        private void MulInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)] * bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)];
            oPtr[i + 1] = xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)] * bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)];
            oPtr[i + 2] = xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)] * bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)];
            oPtr[i + 3] = xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)] * bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)];
        }

        private void DivInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)] / bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)];
            oPtr[i + 1] = xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)] / bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)];
            oPtr[i + 2] = xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)] / bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)];
            oPtr[i + 3] = xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)] / bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)];
        }

        private void MinInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = Mathf.Min( xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)] , bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)] );
            oPtr[i + 1] = Mathf.Min( xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)] , bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)] );
            oPtr[i + 2] = Mathf.Min( xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)] , bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)] );
            oPtr[i + 3] = Mathf.Min( xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)] , bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)] );
        }

        private void MaxInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = Mathf.Max(xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)], bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)]);
            oPtr[i + 1] = Mathf.Max(xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)], bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)]);
            oPtr[i + 2] = Mathf.Max(xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)], bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)]);
            oPtr[i + 3] = Mathf.Max(xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)], bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)]);
        }

        private void GreaterInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = (xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)] > bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)]) ? 1.0f : 0.0f;
            oPtr[i + 1] = (xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)] > bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)]) ? 1.0f : 0.0f;
            oPtr[i + 2] = (xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)] > bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)]) ? 1.0f : 0.0f;
            oPtr[i + 3] = (xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)] > bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)]) ? 1.0f : 0.0f;
        }

        private void GreaterEqualInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = (xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)] >= bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)]) ? 1.0f : 0.0f;
            oPtr[i + 1] = (xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)] >= bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)]) ? 1.0f : 0.0f;
            oPtr[i + 2] = (xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)] >= bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)]) ? 1.0f : 0.0f;
            oPtr[i + 3] = (xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)] >= bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)]) ? 1.0f : 0.0f;
        }

        private void LessInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = (xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)] < bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)]) ? 1.0f : 0.0f;
            oPtr[i + 1] = (xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)] < bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)]) ? 1.0f : 0.0f;
            oPtr[i + 2] = (xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)] < bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)]) ? 1.0f : 0.0f;
            oPtr[i + 3] = (xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)] < bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)]) ? 1.0f : 0.0f;
        }

        private void LessEqualInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = (xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)] <= bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)]) ? 1.0f : 0.0f;
            oPtr[i + 1] = (xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)] <= bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)]) ? 1.0f : 0.0f;
            oPtr[i + 2] = (xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)] <= bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)]) ? 1.0f : 0.0f;
            oPtr[i + 3] = (xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)] <= bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)]) ? 1.0f : 0.0f;
        }

        private void EqualInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = (xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)] == bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)]) ? 1.0f : 0.0f;
            oPtr[i + 1] = (xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)] == bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)]) ? 1.0f : 0.0f;
            oPtr[i + 2] = (xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)] == bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)]) ? 1.0f : 0.0f;
            oPtr[i + 3] = (xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)] == bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)]) ? 1.0f : 0.0f;
        }

        private void LogicalOrInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)]) || Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)])) ? 1.0f : 0.0f;
            oPtr[i + 1] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)]) || Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)])) ? 1.0f : 0.0f;
            oPtr[i + 2] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)]) || Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)])) ? 1.0f : 0.0f;
            oPtr[i + 3] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)]) || Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)])) ? 1.0f : 0.0f;
        }

        private void LogicalAndInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)]) && Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)])) ? 1.0f : 0.0f;
            oPtr[i + 1] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)]) && Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)])) ? 1.0f : 0.0f;
            oPtr[i + 2] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)]) && Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)])) ? 1.0f : 0.0f;
            oPtr[i + 3] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)]) && Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)])) ? 1.0f : 0.0f;
        }

        private void LogicalXorInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)]) ^ Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)])) ? 1.0f : 0.0f;
            oPtr[i + 1] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)]) ^ Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)])) ? 1.0f : 0.0f;
            oPtr[i + 2] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)]) ^ Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)])) ? 1.0f : 0.0f;
            oPtr[i + 3] = (Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)]) ^ Convert.ToBoolean(bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)])) ? 1.0f : 0.0f;
        }

        private void WhereInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            int b0 = 0, h0 = 0, w0 = 0, ch0 = 0;
            int b1 = 0, h1 = 0, w1 = 0, ch1 = 0;
            int b2 = 0, h2 = 0, w2 = 0, ch2 = 0;
            int b3 = 0, h3 = 0, w3 = 0, ch3 = 0;
            oShape.GetPositionsFromIndex(i + 0, ref b0, ref h0, ref w0, ref ch0);
            oShape.GetPositionsFromIndex(i + 1, ref b1, ref h1, ref w1, ref ch1);
            oShape.GetPositionsFromIndex(i + 2, ref b2, ref h2, ref w2, ref ch2);
            oShape.GetPositionsFromIndex(i + 3, ref b3, ref h3, ref w3, ref ch3);

            oPtr[i + 0] = Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b0, h0, w0, ch0)]) ? sPtr[sShape.IndexWithBroadcast(b0, h0, w0, ch0)] : bPtr[bShape.IndexWithBroadcast(b0, h0, w0, ch0)];
            oPtr[i + 1] = Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b1, h1, w1, ch1)]) ? sPtr[sShape.IndexWithBroadcast(b1, h1, w1, ch1)] : bPtr[bShape.IndexWithBroadcast(b1, h1, w1, ch1)];
            oPtr[i + 2] = Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b2, h2, w2, ch2)]) ? sPtr[sShape.IndexWithBroadcast(b2, h2, w2, ch2)] : bPtr[bShape.IndexWithBroadcast(b2, h2, w2, ch2)];
            oPtr[i + 3] = Convert.ToBoolean(xPtr[xShape.IndexWithBroadcast(b3, h3, w3, ch3)]) ? sPtr[sShape.IndexWithBroadcast(b3, h3, w3, ch3)] : bPtr[bShape.IndexWithBroadcast(b3, h3, w3, ch3)];
        }

        private void AddInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = xPtr[(i + 0) % xLen] + bPtr[(i + 0) % bLen];
            oPtr[i + 1] = xPtr[(i + 1) % xLen] + bPtr[(i + 1) % bLen];
            oPtr[i + 2] = xPtr[(i + 2) % xLen] + bPtr[(i + 2) % bLen];
            oPtr[i + 3] = xPtr[(i + 3) % xLen] + bPtr[(i + 3) % bLen];
        }

        private void SubInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = xPtr[(i + 0) % xLen] - bPtr[(i + 0) % bLen];
            oPtr[i + 1] = xPtr[(i + 1) % xLen] - bPtr[(i + 1) % bLen];
            oPtr[i + 2] = xPtr[(i + 2) % xLen] - bPtr[(i + 2) % bLen];
            oPtr[i + 3] = xPtr[(i + 3) % xLen] - bPtr[(i + 3) % bLen];
        }

        private void MulInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = xPtr[(i + 0) % xLen] * bPtr[(i + 0) % bLen];
            oPtr[i + 1] = xPtr[(i + 1) % xLen] * bPtr[(i + 1) % bLen];
            oPtr[i + 2] = xPtr[(i + 2) % xLen] * bPtr[(i + 2) % bLen];
            oPtr[i + 3] = xPtr[(i + 3) % xLen] * bPtr[(i + 3) % bLen];
        }

        private void DivInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = xPtr[(i + 0) % xLen] / bPtr[(i + 0) % bLen];
            oPtr[i + 1] = xPtr[(i + 1) % xLen] / bPtr[(i + 1) % bLen];
            oPtr[i + 2] = xPtr[(i + 2) % xLen] / bPtr[(i + 2) % bLen];
            oPtr[i + 3] = xPtr[(i + 3) % xLen] / bPtr[(i + 3) % bLen];
        }

        private void MinInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = Mathf.Min(xPtr[(i + 0) % xLen], bPtr[(i + 0) % bLen]);
            oPtr[i + 1] = Mathf.Min(xPtr[(i + 1) % xLen], bPtr[(i + 1) % bLen]);
            oPtr[i + 2] = Mathf.Min(xPtr[(i + 2) % xLen], bPtr[(i + 2) % bLen]);
            oPtr[i + 3] = Mathf.Min(xPtr[(i + 3) % xLen], bPtr[(i + 3) % bLen]);
        }

        private void MaxInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = Mathf.Max(xPtr[(i + 0) % xLen], bPtr[(i + 0) % bLen]);
            oPtr[i + 1] = Mathf.Max(xPtr[(i + 1) % xLen], bPtr[(i + 1) % bLen]);
            oPtr[i + 2] = Mathf.Max(xPtr[(i + 2) % xLen], bPtr[(i + 2) % bLen]);
            oPtr[i + 3] = Mathf.Max(xPtr[(i + 3) % xLen], bPtr[(i + 3) % bLen]);
        }

        private void GreaterInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = (xPtr[(i + 0) % xLen] > bPtr[(i + 0) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 1] = (xPtr[(i + 1) % xLen] > bPtr[(i + 1) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 2] = (xPtr[(i + 2) % xLen] > bPtr[(i + 2) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 3] = (xPtr[(i + 3) % xLen] > bPtr[(i + 3) % bLen]) ? 1.0f : 0.0f;
        }

        private void GreaterEqualInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = (xPtr[(i + 0) % xLen] >= bPtr[(i + 0) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 1] = (xPtr[(i + 1) % xLen] >= bPtr[(i + 1) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 2] = (xPtr[(i + 2) % xLen] >= bPtr[(i + 2) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 3] = (xPtr[(i + 3) % xLen] >= bPtr[(i + 3) % bLen]) ? 1.0f : 0.0f;
        }

        private void LessInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = (xPtr[(i + 0) % xLen] < bPtr[(i + 0) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 1] = (xPtr[(i + 1) % xLen] < bPtr[(i + 1) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 2] = (xPtr[(i + 2) % xLen] < bPtr[(i + 2) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 3] = (xPtr[(i + 3) % xLen] < bPtr[(i + 3) % bLen]) ? 1.0f : 0.0f;
        }

        private void LessEqualInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = (xPtr[(i + 0) % xLen] <= bPtr[(i + 0) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 1] = (xPtr[(i + 1) % xLen] <= bPtr[(i + 1) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 2] = (xPtr[(i + 2) % xLen] <= bPtr[(i + 2) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 3] = (xPtr[(i + 3) % xLen] <= bPtr[(i + 3) % bLen]) ? 1.0f : 0.0f;
        }

        private void EqualInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = (xPtr[(i + 0) % xLen] == bPtr[(i + 0) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 1] = (xPtr[(i + 1) % xLen] == bPtr[(i + 1) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 2] = (xPtr[(i + 2) % xLen] == bPtr[(i + 2) % bLen]) ? 1.0f : 0.0f;
            oPtr[i + 3] = (xPtr[(i + 3) % xLen] == bPtr[(i + 3) % bLen]) ? 1.0f : 0.0f;
        }

        private void LogicalOrInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = (Convert.ToBoolean(xPtr[(i + 0) % xLen]) || Convert.ToBoolean(bPtr[(i + 0) % bLen])) ? 1.0f : 0.0f;
            oPtr[i + 1] = (Convert.ToBoolean(xPtr[(i + 1) % xLen]) || Convert.ToBoolean(bPtr[(i + 1) % bLen])) ? 1.0f : 0.0f;
            oPtr[i + 2] = (Convert.ToBoolean(xPtr[(i + 2) % xLen]) || Convert.ToBoolean(bPtr[(i + 2) % bLen])) ? 1.0f : 0.0f;
            oPtr[i + 3] = (Convert.ToBoolean(xPtr[(i + 3) % xLen]) || Convert.ToBoolean(bPtr[(i + 3) % bLen])) ? 1.0f : 0.0f;
        }

        private void LogicalAndInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = (Convert.ToBoolean(xPtr[(i + 0) % xLen]) && Convert.ToBoolean(bPtr[(i + 0) % bLen])) ? 1.0f : 0.0f;
            oPtr[i + 1] = (Convert.ToBoolean(xPtr[(i + 1) % xLen]) && Convert.ToBoolean(bPtr[(i + 1) % bLen])) ? 1.0f : 0.0f;
            oPtr[i + 2] = (Convert.ToBoolean(xPtr[(i + 2) % xLen]) && Convert.ToBoolean(bPtr[(i + 2) % bLen])) ? 1.0f : 0.0f;
            oPtr[i + 3] = (Convert.ToBoolean(xPtr[(i + 3) % xLen]) && Convert.ToBoolean(bPtr[(i + 3) % bLen])) ? 1.0f : 0.0f;
        }

        private void LogicalXorInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = (Convert.ToBoolean(xPtr[(i + 0) % xLen]) ^ Convert.ToBoolean(bPtr[(i + 0) % bLen])) ? 1.0f : 0.0f;
            oPtr[i + 1] = (Convert.ToBoolean(xPtr[(i + 1) % xLen]) ^ Convert.ToBoolean(bPtr[(i + 1) % bLen])) ? 1.0f : 0.0f;
            oPtr[i + 2] = (Convert.ToBoolean(xPtr[(i + 2) % xLen]) ^ Convert.ToBoolean(bPtr[(i + 2) % bLen])) ? 1.0f : 0.0f;
            oPtr[i + 3] = (Convert.ToBoolean(xPtr[(i + 3) % xLen]) ^ Convert.ToBoolean(bPtr[(i + 3) % bLen])) ? 1.0f : 0.0f;
        }

        private void LogicalNotInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = Convert.ToBoolean(xPtr[i + 0]) ? 0.0f : 1.0f;
            oPtr[i + 1] = Convert.ToBoolean(xPtr[i + 1]) ? 0.0f : 1.0f;
            oPtr[i + 2] = Convert.ToBoolean(xPtr[i + 2]) ? 0.0f : 1.0f;
            oPtr[i + 3] = Convert.ToBoolean(xPtr[i + 3]) ? 0.0f : 1.0f;
        }

        private void SignInnerLoop(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = (xPtr[i + 0] > 0) ? 1.0f : ((xPtr[i + 0] < 0) ? -1.0f : 0.0f);
            oPtr[i + 1] = (xPtr[i + 1] > 0) ? 1.0f : ((xPtr[i + 1] < 0) ? -1.0f : 0.0f);
            oPtr[i + 2] = (xPtr[i + 2] > 0) ? 1.0f : ((xPtr[i + 2] < 0) ? -1.0f : 0.0f);
            oPtr[i + 3] = (xPtr[i + 3] > 0) ? 1.0f : ((xPtr[i + 3] < 0) ? -1.0f : 0.0f);
        }

        private void WhereInnerLoopNoBroadcast(long n)
        {
            int i = (int)n * unrollSize;

            oPtr[i + 0] = Convert.ToBoolean(xPtr[(i + 0) % xLen]) ? sPtr[(i + 0) % bLen] : bPtr[(i + 0) % bLen];
            oPtr[i + 1] = Convert.ToBoolean(xPtr[(i + 1) % xLen]) ? sPtr[(i + 1) % bLen] : bPtr[(i + 1) % bLen];
            oPtr[i + 2] = Convert.ToBoolean(xPtr[(i + 2) % xLen]) ? sPtr[(i + 2) % bLen] : bPtr[(i + 2) % bLen];
            oPtr[i + 3] = Convert.ToBoolean(xPtr[(i + 3) % xLen]) ? sPtr[(i + 3) % bLen] : bPtr[(i + 3) % bLen];
        }

        private void ScaleBiasInnerLoop(long n)
        {
            var offset = n * unrollSize;
            float* baseXPtr = xPtr + offset;
            float* baseOPtr = oPtr + offset;

            float v0 = baseXPtr[0];
            float v1 = baseXPtr[1];
            float v2 = baseXPtr[2];
            float v3 = baseXPtr[3];

            float s0 = sPtr[(offset + 0) % sLen];
            float s1 = sPtr[(offset + 1) % sLen];
            float s2 = sPtr[(offset + 2) % sLen];
            float s3 = sPtr[(offset + 3) % sLen];

            float b0 = bPtr[(offset + 0) % bLen];
            float b1 = bPtr[(offset + 1) % bLen];
            float b2 = bPtr[(offset + 2) % bLen];
            float b3 = bPtr[(offset + 3) % bLen];

            v0 = s0 * v0 + b0;
            v1 = s1 * v1 + b1;
            v2 = s2 * v2 + b2;
            v3 = s3 * v3 + b3;

            baseOPtr[0] = v0;
            baseOPtr[1] = v1;
            baseOPtr[2] = v2;
            baseOPtr[3] = v3;
        }

        private float Add(float a, float b)
        {
            return a + b;
        }
        private float Sub(float a, float b)
        {
            return a - b;
        }
        private float Mul(float a, float b)
        {
            return a * b;
        }
        private float Div(float a, float b)
        {
            return a / b;
        }
        private float Min(float a, float b)
        {
            return Mathf.Min(a, b);
        }
        private float Max(float a, float b)
        {
            return Mathf.Max(a, b);
        }
        private float Greater(float a, float b)
        {
            return Convert.ToSingle(a > b);
        }
        private float GreaterEqual(float a, float b)
        {
            return Convert.ToSingle(a >= b);
        }
        private float Less(float a, float b)
        {
            return Convert.ToSingle(a < b);
        }
        private float LessEqual(float a, float b)
        {
            return Convert.ToSingle(a <= b);
        }
        private float Equal(float a, float b)
        {
            return Convert.ToSingle(a == b);
        }
        private float LogicalOr(float a, float b)
        {
            return Convert.ToSingle(Convert.ToBoolean(a) || Convert.ToBoolean(b));
        }
        private float LogicalAnd(float a, float b)
        {
            return Convert.ToSingle(Convert.ToBoolean(a) && Convert.ToBoolean(b));
        }
        private float LogicalXor(float a, float b)
        {
            return Convert.ToSingle(Convert.ToBoolean(a) ^ Convert.ToBoolean(b));
        }
        private float LogicalNot(float a)
        {
            return Convert.ToSingle(!Convert.ToBoolean(a));
        }
        private float Sign(float a)
        {
            return (a > 0) ? 1.0f : ((a < 0) ? -1.0f : 0.0f);
        }
        private float Where(float c, float a, float b)
        {
            return Convert.ToBoolean(c) ? a : b;
        }
    }


} // namespace Barracuda
