// <auto-generated>
//     Generated by the protocol buffer compiler.  DO NOT EDIT!
//     source: mediapipe/modules/objectron/calculators/lift_2d_frame_annotation_to_3d_calculator.proto
// </auto-generated>
#pragma warning disable 1591, 0612, 3021
#region Designer generated code

using pb = global::Google.Protobuf;
using pbc = global::Google.Protobuf.Collections;
using pbr = global::Google.Protobuf.Reflection;
using scg = global::System.Collections.Generic;
namespace Mediapipe {

  /// <summary>Holder for reflection information generated from mediapipe/modules/objectron/calculators/lift_2d_frame_annotation_to_3d_calculator.proto</summary>
  public static partial class Lift2DFrameAnnotationTo3DCalculatorReflection {

    #region Descriptor
    /// <summary>File descriptor for mediapipe/modules/objectron/calculators/lift_2d_frame_annotation_to_3d_calculator.proto</summary>
    public static pbr::FileDescriptor Descriptor {
      get { return descriptor; }
    }
    private static pbr::FileDescriptor descriptor;

    static Lift2DFrameAnnotationTo3DCalculatorReflection() {
      byte[] descriptorData = global::System.Convert.FromBase64String(
          string.Concat(
            "CldtZWRpYXBpcGUvbW9kdWxlcy9vYmplY3Ryb24vY2FsY3VsYXRvcnMvbGlm",
            "dF8yZF9mcmFtZV9hbm5vdGF0aW9uX3RvXzNkX2NhbGN1bGF0b3IucHJvdG8S",
            "CW1lZGlhcGlwZRokbWVkaWFwaXBlL2ZyYW1ld29yay9jYWxjdWxhdG9yLnBy",
            "b3RvGkNtZWRpYXBpcGUvbW9kdWxlcy9vYmplY3Ryb24vY2FsY3VsYXRvcnMv",
            "YmVsaWVmX2RlY29kZXJfY29uZmlnLnByb3RvItoCCipMaWZ0MkRGcmFtZUFu",
            "bm90YXRpb25UbzNEQ2FsY3VsYXRvck9wdGlvbnMSNgoOZGVjb2Rlcl9jb25m",
            "aWcYASABKAsyHi5tZWRpYXBpcGUuQmVsaWVmRGVjb2RlckNvbmZpZxIdChJu",
            "b3JtYWxpemVkX2ZvY2FsX3gYAiABKAI6ATESHQoSbm9ybWFsaXplZF9mb2Nh",
            "bF95GAMgASgCOgExEicKHG5vcm1hbGl6ZWRfcHJpbmNpcGFsX3BvaW50X3gY",
            "BCABKAI6ATASJwocbm9ybWFsaXplZF9wcmluY2lwYWxfcG9pbnRfeRgFIAEo",
            "AjoBMDJkCgNleHQSHC5tZWRpYXBpcGUuQ2FsY3VsYXRvck9wdGlvbnMYjKyu",
            "igEgASgLMjUubWVkaWFwaXBlLkxpZnQyREZyYW1lQW5ub3RhdGlvblRvM0RD",
            "YWxjdWxhdG9yT3B0aW9ucw=="));
      descriptor = pbr::FileDescriptor.FromGeneratedCode(descriptorData,
          new pbr::FileDescriptor[] { global::Mediapipe.CalculatorReflection.Descriptor, global::Mediapipe.BeliefDecoderConfigReflection.Descriptor, },
          new pbr::GeneratedClrTypeInfo(null, null, new pbr::GeneratedClrTypeInfo[] {
            new pbr::GeneratedClrTypeInfo(typeof(global::Mediapipe.Lift2DFrameAnnotationTo3DCalculatorOptions), global::Mediapipe.Lift2DFrameAnnotationTo3DCalculatorOptions.Parser, new[]{ "DecoderConfig", "NormalizedFocalX", "NormalizedFocalY", "NormalizedPrincipalPointX", "NormalizedPrincipalPointY" }, null, null, new pb::Extension[] { global::Mediapipe.Lift2DFrameAnnotationTo3DCalculatorOptions.Extensions.Ext }, null)
          }));
    }
    #endregion

  }
  #region Messages
  public sealed partial class Lift2DFrameAnnotationTo3DCalculatorOptions : pb::IMessage<Lift2DFrameAnnotationTo3DCalculatorOptions>
  #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      , pb::IBufferMessage
  #endif
  {
    private static readonly pb::MessageParser<Lift2DFrameAnnotationTo3DCalculatorOptions> _parser = new pb::MessageParser<Lift2DFrameAnnotationTo3DCalculatorOptions>(() => new Lift2DFrameAnnotationTo3DCalculatorOptions());
    private pb::UnknownFieldSet _unknownFields;
    private int _hasBits0;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pb::MessageParser<Lift2DFrameAnnotationTo3DCalculatorOptions> Parser { get { return _parser; } }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static pbr::MessageDescriptor Descriptor {
      get { return global::Mediapipe.Lift2DFrameAnnotationTo3DCalculatorReflection.Descriptor.MessageTypes[0]; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    pbr::MessageDescriptor pb::IMessage.Descriptor {
      get { return Descriptor; }
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public Lift2DFrameAnnotationTo3DCalculatorOptions() {
      OnConstruction();
    }

    partial void OnConstruction();

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public Lift2DFrameAnnotationTo3DCalculatorOptions(Lift2DFrameAnnotationTo3DCalculatorOptions other) : this() {
      _hasBits0 = other._hasBits0;
      decoderConfig_ = other.decoderConfig_ != null ? other.decoderConfig_.Clone() : null;
      normalizedFocalX_ = other.normalizedFocalX_;
      normalizedFocalY_ = other.normalizedFocalY_;
      normalizedPrincipalPointX_ = other.normalizedPrincipalPointX_;
      normalizedPrincipalPointY_ = other.normalizedPrincipalPointY_;
      _unknownFields = pb::UnknownFieldSet.Clone(other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public Lift2DFrameAnnotationTo3DCalculatorOptions Clone() {
      return new Lift2DFrameAnnotationTo3DCalculatorOptions(this);
    }

    /// <summary>Field number for the "decoder_config" field.</summary>
    public const int DecoderConfigFieldNumber = 1;
    private global::Mediapipe.BeliefDecoderConfig decoderConfig_;
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public global::Mediapipe.BeliefDecoderConfig DecoderConfig {
      get { return decoderConfig_; }
      set {
        decoderConfig_ = value;
      }
    }

    /// <summary>Field number for the "normalized_focal_x" field.</summary>
    public const int NormalizedFocalXFieldNumber = 2;
    private readonly static float NormalizedFocalXDefaultValue = 1F;

    private float normalizedFocalX_;
    /// <summary>
    /// Camera focal length along x, normalized by width/2.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public float NormalizedFocalX {
      get { if ((_hasBits0 & 1) != 0) { return normalizedFocalX_; } else { return NormalizedFocalXDefaultValue; } }
      set {
        _hasBits0 |= 1;
        normalizedFocalX_ = value;
      }
    }
    /// <summary>Gets whether the "normalized_focal_x" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasNormalizedFocalX {
      get { return (_hasBits0 & 1) != 0; }
    }
    /// <summary>Clears the value of the "normalized_focal_x" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearNormalizedFocalX() {
      _hasBits0 &= ~1;
    }

    /// <summary>Field number for the "normalized_focal_y" field.</summary>
    public const int NormalizedFocalYFieldNumber = 3;
    private readonly static float NormalizedFocalYDefaultValue = 1F;

    private float normalizedFocalY_;
    /// <summary>
    /// Camera focal length along y, normalized by height/2.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public float NormalizedFocalY {
      get { if ((_hasBits0 & 2) != 0) { return normalizedFocalY_; } else { return NormalizedFocalYDefaultValue; } }
      set {
        _hasBits0 |= 2;
        normalizedFocalY_ = value;
      }
    }
    /// <summary>Gets whether the "normalized_focal_y" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasNormalizedFocalY {
      get { return (_hasBits0 & 2) != 0; }
    }
    /// <summary>Clears the value of the "normalized_focal_y" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearNormalizedFocalY() {
      _hasBits0 &= ~2;
    }

    /// <summary>Field number for the "normalized_principal_point_x" field.</summary>
    public const int NormalizedPrincipalPointXFieldNumber = 4;
    private readonly static float NormalizedPrincipalPointXDefaultValue = 0F;

    private float normalizedPrincipalPointX_;
    /// <summary>
    /// Camera principle point x, normalized by width/2, origin is image center.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public float NormalizedPrincipalPointX {
      get { if ((_hasBits0 & 4) != 0) { return normalizedPrincipalPointX_; } else { return NormalizedPrincipalPointXDefaultValue; } }
      set {
        _hasBits0 |= 4;
        normalizedPrincipalPointX_ = value;
      }
    }
    /// <summary>Gets whether the "normalized_principal_point_x" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasNormalizedPrincipalPointX {
      get { return (_hasBits0 & 4) != 0; }
    }
    /// <summary>Clears the value of the "normalized_principal_point_x" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearNormalizedPrincipalPointX() {
      _hasBits0 &= ~4;
    }

    /// <summary>Field number for the "normalized_principal_point_y" field.</summary>
    public const int NormalizedPrincipalPointYFieldNumber = 5;
    private readonly static float NormalizedPrincipalPointYDefaultValue = 0F;

    private float normalizedPrincipalPointY_;
    /// <summary>
    /// Camera principle point y, normalized by height/2, origin is image center.
    /// </summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public float NormalizedPrincipalPointY {
      get { if ((_hasBits0 & 8) != 0) { return normalizedPrincipalPointY_; } else { return NormalizedPrincipalPointYDefaultValue; } }
      set {
        _hasBits0 |= 8;
        normalizedPrincipalPointY_ = value;
      }
    }
    /// <summary>Gets whether the "normalized_principal_point_y" field is set</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool HasNormalizedPrincipalPointY {
      get { return (_hasBits0 & 8) != 0; }
    }
    /// <summary>Clears the value of the "normalized_principal_point_y" field</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void ClearNormalizedPrincipalPointY() {
      _hasBits0 &= ~8;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override bool Equals(object other) {
      return Equals(other as Lift2DFrameAnnotationTo3DCalculatorOptions);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public bool Equals(Lift2DFrameAnnotationTo3DCalculatorOptions other) {
      if (ReferenceEquals(other, null)) {
        return false;
      }
      if (ReferenceEquals(other, this)) {
        return true;
      }
      if (!object.Equals(DecoderConfig, other.DecoderConfig)) return false;
      if (!pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.Equals(NormalizedFocalX, other.NormalizedFocalX)) return false;
      if (!pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.Equals(NormalizedFocalY, other.NormalizedFocalY)) return false;
      if (!pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.Equals(NormalizedPrincipalPointX, other.NormalizedPrincipalPointX)) return false;
      if (!pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.Equals(NormalizedPrincipalPointY, other.NormalizedPrincipalPointY)) return false;
      return Equals(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override int GetHashCode() {
      int hash = 1;
      if (decoderConfig_ != null) hash ^= DecoderConfig.GetHashCode();
      if (HasNormalizedFocalX) hash ^= pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.GetHashCode(NormalizedFocalX);
      if (HasNormalizedFocalY) hash ^= pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.GetHashCode(NormalizedFocalY);
      if (HasNormalizedPrincipalPointX) hash ^= pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.GetHashCode(NormalizedPrincipalPointX);
      if (HasNormalizedPrincipalPointY) hash ^= pbc::ProtobufEqualityComparers.BitwiseSingleEqualityComparer.GetHashCode(NormalizedPrincipalPointY);
      if (_unknownFields != null) {
        hash ^= _unknownFields.GetHashCode();
      }
      return hash;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public override string ToString() {
      return pb::JsonFormatter.ToDiagnosticString(this);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void WriteTo(pb::CodedOutputStream output) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      output.WriteRawMessage(this);
    #else
      if (decoderConfig_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(DecoderConfig);
      }
      if (HasNormalizedFocalX) {
        output.WriteRawTag(21);
        output.WriteFloat(NormalizedFocalX);
      }
      if (HasNormalizedFocalY) {
        output.WriteRawTag(29);
        output.WriteFloat(NormalizedFocalY);
      }
      if (HasNormalizedPrincipalPointX) {
        output.WriteRawTag(37);
        output.WriteFloat(NormalizedPrincipalPointX);
      }
      if (HasNormalizedPrincipalPointY) {
        output.WriteRawTag(45);
        output.WriteFloat(NormalizedPrincipalPointY);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(output);
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalWriteTo(ref pb::WriteContext output) {
      if (decoderConfig_ != null) {
        output.WriteRawTag(10);
        output.WriteMessage(DecoderConfig);
      }
      if (HasNormalizedFocalX) {
        output.WriteRawTag(21);
        output.WriteFloat(NormalizedFocalX);
      }
      if (HasNormalizedFocalY) {
        output.WriteRawTag(29);
        output.WriteFloat(NormalizedFocalY);
      }
      if (HasNormalizedPrincipalPointX) {
        output.WriteRawTag(37);
        output.WriteFloat(NormalizedPrincipalPointX);
      }
      if (HasNormalizedPrincipalPointY) {
        output.WriteRawTag(45);
        output.WriteFloat(NormalizedPrincipalPointY);
      }
      if (_unknownFields != null) {
        _unknownFields.WriteTo(ref output);
      }
    }
    #endif

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public int CalculateSize() {
      int size = 0;
      if (decoderConfig_ != null) {
        size += 1 + pb::CodedOutputStream.ComputeMessageSize(DecoderConfig);
      }
      if (HasNormalizedFocalX) {
        size += 1 + 4;
      }
      if (HasNormalizedFocalY) {
        size += 1 + 4;
      }
      if (HasNormalizedPrincipalPointX) {
        size += 1 + 4;
      }
      if (HasNormalizedPrincipalPointY) {
        size += 1 + 4;
      }
      if (_unknownFields != null) {
        size += _unknownFields.CalculateSize();
      }
      return size;
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(Lift2DFrameAnnotationTo3DCalculatorOptions other) {
      if (other == null) {
        return;
      }
      if (other.decoderConfig_ != null) {
        if (decoderConfig_ == null) {
          DecoderConfig = new global::Mediapipe.BeliefDecoderConfig();
        }
        DecoderConfig.MergeFrom(other.DecoderConfig);
      }
      if (other.HasNormalizedFocalX) {
        NormalizedFocalX = other.NormalizedFocalX;
      }
      if (other.HasNormalizedFocalY) {
        NormalizedFocalY = other.NormalizedFocalY;
      }
      if (other.HasNormalizedPrincipalPointX) {
        NormalizedPrincipalPointX = other.NormalizedPrincipalPointX;
      }
      if (other.HasNormalizedPrincipalPointY) {
        NormalizedPrincipalPointY = other.NormalizedPrincipalPointY;
      }
      _unknownFields = pb::UnknownFieldSet.MergeFrom(_unknownFields, other._unknownFields);
    }

    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public void MergeFrom(pb::CodedInputStream input) {
    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
      input.ReadRawMessage(this);
    #else
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, input);
            break;
          case 10: {
            if (decoderConfig_ == null) {
              DecoderConfig = new global::Mediapipe.BeliefDecoderConfig();
            }
            input.ReadMessage(DecoderConfig);
            break;
          }
          case 21: {
            NormalizedFocalX = input.ReadFloat();
            break;
          }
          case 29: {
            NormalizedFocalY = input.ReadFloat();
            break;
          }
          case 37: {
            NormalizedPrincipalPointX = input.ReadFloat();
            break;
          }
          case 45: {
            NormalizedPrincipalPointY = input.ReadFloat();
            break;
          }
        }
      }
    #endif
    }

    #if !GOOGLE_PROTOBUF_REFSTRUCT_COMPATIBILITY_MODE
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    void pb::IBufferMessage.InternalMergeFrom(ref pb::ParseContext input) {
      uint tag;
      while ((tag = input.ReadTag()) != 0) {
        switch(tag) {
          default:
            _unknownFields = pb::UnknownFieldSet.MergeFieldFrom(_unknownFields, ref input);
            break;
          case 10: {
            if (decoderConfig_ == null) {
              DecoderConfig = new global::Mediapipe.BeliefDecoderConfig();
            }
            input.ReadMessage(DecoderConfig);
            break;
          }
          case 21: {
            NormalizedFocalX = input.ReadFloat();
            break;
          }
          case 29: {
            NormalizedFocalY = input.ReadFloat();
            break;
          }
          case 37: {
            NormalizedPrincipalPointX = input.ReadFloat();
            break;
          }
          case 45: {
            NormalizedPrincipalPointY = input.ReadFloat();
            break;
          }
        }
      }
    }
    #endif

    #region Extensions
    /// <summary>Container for extensions for other messages declared in the Lift2DFrameAnnotationTo3DCalculatorOptions message type.</summary>
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute]
    [global::System.CodeDom.Compiler.GeneratedCode("protoc", null)]
    public static partial class Extensions {
      public static readonly pb::Extension<global::Mediapipe.CalculatorOptions, global::Mediapipe.Lift2DFrameAnnotationTo3DCalculatorOptions> Ext =
        new pb::Extension<global::Mediapipe.CalculatorOptions, global::Mediapipe.Lift2DFrameAnnotationTo3DCalculatorOptions>(290166284, pb::FieldCodec.ForMessage(2321330274, global::Mediapipe.Lift2DFrameAnnotationTo3DCalculatorOptions.Parser));
    }
    #endregion

  }

  #endregion

}

#endregion Designer generated code
