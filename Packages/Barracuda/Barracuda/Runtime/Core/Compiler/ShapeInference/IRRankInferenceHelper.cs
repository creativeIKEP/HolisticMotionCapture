using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;

namespace Unity.Barracuda.Compiler.IRShapeInferenceHelper
{
    internal class RankInference
    {
        static public int? InferOutputRank(Layer layer, int?[] inputRanks, TensorShape?[] inputShapes)
        {
            switch (layer.type)
            {
                case Layer.Type.Dense:
                {
                    Assert.AreEqual(inputRanks.Length, 1, "InferOutputRank.Dense inputRanks.Length");
                    return 2;
                }
                case Layer.Type.MatMul:
                {
                    if (inputRanks.Length != 2)
                        return null;
                    if (inputRanks.Any(x => x == null))
                        return null;
                    return inputRanks.Max();
                }
                case Layer.Type.Conv3D:
                {
                    if (inputRanks[0] == null)
                        return null;

                    Assert.AreEqual(inputRanks.Length, 1, "InferOutputRank.*Conv3D* inputRanks.Length"); Assert.IsTrue(inputRanks[0].Value >= 2 && inputRanks[0].Value <= 5, "InferOutputRank.*Conv3D* inputRanks");
                    return inputRanks[0];
                }
                case Layer.Type.Conv2D:
                case Layer.Type.DepthwiseConv2D:
                case Layer.Type.Conv2DTrans:
                {
                    if (inputRanks[0] == null)
                        return null;

                    Assert.AreEqual(inputRanks.Length, 1, "InferOutputRank.*Conv2D* inputRanks.Length"); Assert.IsTrue(inputRanks[0].Value >= 2 && inputRanks[0].Value <= 4, "InferOutputRank.*Conv2D* inputRanks"); // conv1D/2D are done via conv2D
                    return inputRanks[0];
                }
                case Layer.Type.DepthToSpace:
                case Layer.Type.SpaceToDepth:
                {
                    if (inputRanks[0] == null)
                        return null;

                    Assert.AreEqual(inputRanks.Length, 1, "InferOutputRank.ToDepth/Space inputRanks.Length"); Assert.AreEqual(inputRanks[0].Value, 4, "InferOutputRank.ToDepth/Space inputRanks");
                    return 4;
                }
                case Layer.Type.Upsample3D:
                {
                    if (inputRanks[0] == null)
                        return null;

                    Assert.AreEqual(inputRanks[0].Value, 5, "InferOutputRank.*Upsample3D inputRanks");
                    return 5;
                }
                case Layer.Type.Upsample2D:
                case Layer.Type.Resample2D:
                {
                    if (inputRanks[0] == null)
                        return null;

                    Assert.AreEqual(inputRanks[0].Value, 4, "InferOutputRank.*Upsample2D inputRanks");
                    return 4;
                }
                case Layer.Type.MaxPool2D:
                case Layer.Type.AvgPool2D:
                {
                    if (inputRanks[0] == null)
                        return null;

                    Assert.IsTrue(inputRanks[0].Value == 4 || inputRanks[0].Value == 3, "InferOutputRank.*Pool2D inputRanks");
                    return inputRanks[0];
                }
                case Layer.Type.GlobalMaxPool2D:
                case Layer.Type.GlobalAvgPool2D:
                {
                    if (inputRanks[0] == null)
                        return null;

                    Assert.AreEqual(inputRanks.Length, 1, "InferOutputRank.Global*Pool2D inputRanks.Length"); Assert.IsTrue(inputRanks[0].Value == 4 || inputRanks[0].Value == 3, "InferOutputRank.Global*Pool2D inputRanks");
                    return inputRanks[0];
                }
                case Layer.Type.Pad:
                    return inputRanks[0];
                case Layer.Type.RandomNormal:
                case Layer.Type.RandomUniform:
                {
                    if (layer.pool.Length > 0)
                        return layer.pool.Length;
                    else
                    {
                        Assert.AreEqual(inputRanks.Length, 1, "InferOutputRank.*Random inputRanks.Length");
                        return inputRanks[0];
                    }
                }
                case Layer.Type.Multinomial:
                    return 2;
                case Layer.Type.OneHot:
                {
                    if (inputRanks[0] == null)
                        return null;

                    Assert.AreEqual(inputRanks.Length, 1, "InferOutputRank.OneHot inputRanks.Length");
                    return inputRanks[0] + 1;
                }
                case Layer.Type.RoiAlign:
                    return 4;
                case Layer.Type.LSTM:
                    return 4;
                case Layer.Type.Add:
                case Layer.Type.Sub:
                case Layer.Type.Mul:
                case Layer.Type.Div:
                case Layer.Type.Pow:
                case Layer.Type.Min:
                case Layer.Type.Max:
                case Layer.Type.Mean:
                case Layer.Type.Greater:
                case Layer.Type.GreaterEqual:
                case Layer.Type.Less:
                case Layer.Type.LessEqual:
                case Layer.Type.Equal:
                case Layer.Type.LogicalOr:
                case Layer.Type.LogicalAnd:
                case Layer.Type.LogicalXor:
                {
                    if (inputRanks.Any(x => x == null))
                        return null;
                    return inputRanks.Max();
                }
                case Layer.Type.Range:
                {
                    return 1;
                }
                case Layer.Type.ReduceL1:
                case Layer.Type.ReduceL2:
                case Layer.Type.ReduceLogSum:
                case Layer.Type.ReduceLogSumExp:
                case Layer.Type.ReduceMax:
                case Layer.Type.ReduceMean:
                case Layer.Type.ReduceMin:
                case Layer.Type.ReduceProd:
                case Layer.Type.ReduceSum:
                case Layer.Type.ReduceSumSquare:
                case Layer.Type.ArgMax:
                case Layer.Type.ArgMin:
                {
                    if (inputRanks[0] == null)
                        return null;
                    if (layer.alpha != 1.0f)
                        return inputRanks[0] - 1;
                    else
                        return inputRanks[0];
                }
                case Layer.Type.Flatten:
                    return 2;
                case Layer.Type.ConstantOfShape:
                {
                    if(layer.axis == 1)
                        return inputRanks[0];

                    if (inputRanks.Length == 1)
                    {
                        if (inputShapes[0] != null)
                            return (inputShapes[0].Value)[TensorShape.DataBatch];
                        else
                            return null;
                    }
                    else
                        return layer.pool.Length;
                }
                case Layer.Type.Reshape:
                {
                    if (inputShapes.Length == 2 && inputShapes[1] != null)
                        return (inputShapes[1].Value)[TensorShape.DataBatch];

                    if (inputRanks.Length > 1)
                        // shape is in the tensor and calculated at runtime, so we can't know it
                        return null;

                    if (layer.pad.Length > 0)
                        return layer.pad[0]; // original rank stored here

                    return layer.pool.Length;
                }
                case Layer.Type.Expand:
                {
                    if (inputRanks.Length > 1)
                        return null;

                    if(inputRanks[0] == null)
                        return null;

                    return  Mathf.Max(inputRanks[0].Value, layer.pool.Length);
                }
                case Layer.Type.Transpose:
                    return inputRanks[0];
                case Layer.Type.Gather:
                {
                    if (inputRanks.Length != 2)
                        return null;

                    if (inputRanks[0] == null)
                        return null;

                    if (inputRanks[1] == null)
                        return null;

                    // Gather can implicitly do a squeeze in inputs are single int
                    // we don't but instead append a squeeze op after Gather if that is the case
                    return inputRanks[0] + Mathf.Max(inputRanks[1].Value, 1) - 1;
                }
                case Layer.Type.ScatterND:
                    return inputRanks[0];
                case Layer.Type.TopKIndices:
                case Layer.Type.TopKValues:
                    return inputRanks[0];
                case Layer.Type.NonMaxSuppression:
                    return 2;
                case Layer.Type.NonZero:
                    return 2;
                case Layer.Type.Squeeze:
                {
                    if (inputRanks.Length > 1)
                        return null;

                    if(inputRanks[0] == null)
                        return null;

                    return inputRanks[0].Value - layer.pool.Length;
                }
                case Layer.Type.Unsqueeze:
                {
                    if (inputRanks.Length > 1)
                        return null;

                    if(inputRanks[0] == null)
                        return null;

                    return inputRanks[0].Value + layer.pool.Length;
                }
                case Layer.Type.Concat:
                {
                    if (inputRanks.Any(x => x == null))
                        return null;

                    int rank = 0;

                    for (int i = 0; i < inputRanks.Length; i++)
                    {
                        if (inputRanks[i] != null)
                            rank = Math.Max(rank, inputRanks[i].Value);
                    }

                    return rank;
                }
                case Layer.Type.StridedSlice:
                    // TODO : figure out if slice can produce lower rank output
                    return inputRanks[0];
                case Layer.Type.Tile:
                    return inputRanks[0];
                case Layer.Type.Load:
                {
                    if (layer.datasets[0].length == 1 && layer.axis == 1)
                        return 0; // TODO const float vs [float] maybe override rank in ONNXTensor
                    return layer.axis;
                }
                case Layer.Type.Nop:
                case Layer.Type.ScaleBias:
                case Layer.Type.Normalization:
                case Layer.Type.LRN:
                case Layer.Type.Dropout:
                case Layer.Type.LogicalNot:
                case Layer.Type.Sign:
                case Layer.Type.Where:
                {
                    return inputRanks[0];
                }
                case Layer.Type.Activation:
                {
                    // For convenience we sometimes use layer.pad to store rank for inference purposes (e.g. LSTMs)
                    if (layer.activation == Layer.Activation.None && layer.pad.Length > 0)
                        return layer.pad[0];

                    return inputRanks[0];
                }
                case Layer.Type.Shape:
                    return 1;
                default:
                    return null;
            }
        }

        // TODO merge List&Update***
        public static void UpdateKnownTensorRanks(Model model, IDictionary<string, int?> ranksByName)
        {
            foreach (var l in model.layers)
            {
                TensorShape?[] layerInputShapes = new TensorShape?[l.inputs.Length];
                int?[] layerInputShapeRanks = new int?[l.inputs.Length];
                for (int i = 0; i < l.inputs.Length; i++)
                {
                    ranksByName.TryGetValue(l.inputs[i], out int? irank);
                    layerInputShapeRanks[i] = irank;
                }

                int? outputRank = InferOutputRank(l, layerInputShapeRanks, layerInputShapes);

                if (ranksByName.ContainsKey(l.name) && ranksByName[l.name] != null && outputRank != null)
                    ranksByName[l.name] = Mathf.Max(ranksByName[l.name].Value, outputRank.Value);
                else
                    ranksByName[l.name] = outputRank;
            }
        }

        public static int?[] ListTemporaryTensorRanks(Model model,
            out IDictionary<string, int?> ranksByName)
        {
            Profiler.BeginSample("Barracuda.ListTemporaryTensorRanks");
            var ranks = new List<int?>();
            ranksByName = new Dictionary<string, int?>();
            foreach (var i in model.inputs)
                ranksByName[i.name] = i.rank;

            foreach (var m in model.memories)
                ranksByName.Add(m.input, 3); // [num_directions, batch_size, hidden_size]

            foreach (var l in model.layers)
            {
                TensorShape?[] layerInputShapes = new TensorShape?[l.inputs.Length];
                int?[] layerInputShapeRanks = new int?[l.inputs.Length];

                for (int i = 0; i < l.inputs.Length; i++)
                {
                    ranksByName.TryGetValue(l.inputs[i], out int? irank);

                    layerInputShapeRanks[i] = irank;
                }

                int? outputRank = InferOutputRank(l, layerInputShapeRanks, layerInputShapes);

                ranks.Add(outputRank);
                ranksByName.Add(l.name, outputRank);
            }

            Profiler.EndSample();
            return ranks.ToArray();
        }
    }
}
