Shader "Hidden/HolisticBarracuda/HandVisuallizer"
{
    CGINCLUDE

    #include "UnityCG.cginc"

    #define VERTEX_COUNT 21

    float2 _uiScale;
    float4 _pointColor;
    float _handScoreThreshold;
    StructuredBuffer<float4> _vertices;

    void VertexKeys(uint vid : SV_VertexID,
                    uint iid : SV_InstanceID,
                    out float4 position : SV_Position,
                    out float4 color : COLOR)
    {
        float3 p = _vertices[iid].xyz;

        uint fan = vid / 3;
        uint segment = vid % 3;

        float theta = (fan + segment - 1) * UNITY_PI / 16;
        float radius = (segment > 0) * 0.008;

        p.xy += float2(cos(theta), sin(theta)) * radius;
        p.xy = (2 * p.xy - 1) * _uiScale / _ScreenParams.xy;

        float score =  _vertices[VERTEX_COUNT].x;

        position = float4(p.xy, 0, 1);
        color = (score >= _handScoreThreshold) ? _pointColor : float4(0, 0, 1, 1);
    }

    void VertexBones(uint vid : SV_VertexID,
                     uint iid : SV_InstanceID,
                     out float4 position : SV_Position,
                     out float4 color : COLOR)
    {
        uint finger = iid / 4;
        uint segment = iid % 4;

        uint i = min(4, finger) * 4 + segment + vid;
        uint root = finger > 1 && finger < 5 ? i - 3 : 0;

        i = max(segment, vid) == 0 ? root : i;

        float3 p = _vertices[i].xyz;
        p.xy = (2 * p.xy - 1) * _uiScale / _ScreenParams.xy;

        position = float4(p.xy, 0, 1);
        color = float4(1, 1, 1, 1);
    }

    float4 Fragment(float4 position : SV_Position,
                    float4 color : COLOR) : SV_Target
    {
        return color;
    }

    ENDCG

    SubShader
    {
        ZWrite Off ZTest Always Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex VertexKeys
            #pragma fragment Fragment
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex VertexBones
            #pragma fragment Fragment
            ENDCG
        }
    }
}
