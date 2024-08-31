/*
 * MIT License
 * 
 * Copyright (c) 2024 ikuko
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

Shader "HoshinoLabs/iwaSync3/Realtime Emissive Gamma"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "black" {}
        _AspectRatio ("Aspect Ratio", Float) = 1.777778
        [Toggle(_)] _IsAVProVideo ("Is AVPro Video", Int) = 0
        [Toggle(_)] _IsMirror("Is Mirror", Int) = 1
        [Toggle(_)] _IsMirror("Is Vertical Mirror", Int) = 0
        _EmissiveBoost ("Emissive Boost", Float) = 1.0
        [Toggle(_)] _IsPlaying("Is Playing", Int) = 1
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0
        #pragma shader_feature _EMISSION

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        float _AspectRatio;
        int _IsAVProVideo;
        int _IsMirror;
        int _IsVerticalMirror;
        float _EmissiveBoost;
        int _IsPlaying;
        float _Metallic;
        float _Smoothness;

        struct Input
        {
            float2 uv_MainTex;
        };

        fixed4 _Color;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void vert(inout appdata_full v) {
            v.texcoord.xy = _IsMirror && 0 < dot(cross(UNITY_MATRIX_V[0], UNITY_MATRIX_V[1]), UNITY_MATRIX_V[2])
                ? float2(1 - v.texcoord.x, v.texcoord.y)
                : v.texcoord.xy;
            float aspect = _MainTex_TexelSize.z / _AspectRatio;
            v.texcoord.x = _MainTex_TexelSize.w < aspect
                ? v.texcoord.x
                : ((v.texcoord.x - 0.5) / (aspect / _MainTex_TexelSize.w)) + 0.5;
            v.texcoord.y = _MainTex_TexelSize.w < aspect
                ? ((v.texcoord.y - 0.5) / (_MainTex_TexelSize.w / aspect)) + 0.5
                : v.texcoord.y;
            v.texcoord.y = lerp(v.texcoord.y, 1 - v.texcoord.y, _IsVerticalMirror);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
            c *= !any(IN.uv_MainTex < 0 || 1 < IN.uv_MainTex);
            o.Albedo = fixed3(0, 0, 0);
            if (_IsAVProVideo)
                c.rgb = GammaToLinearSpace(c.rgb);
            o.Emission = c * _EmissiveBoost;
            // Metallic and smoothness come from slider variables
            o.Metallic = lerp(_Metallic, 0.0, _IsPlaying);
            o.Smoothness = lerp(_Smoothness, 0.0, _IsPlaying);
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
    CustomEditor "HoshinoLabs.IwaSync3.RealtimeEmissiveGammaShaderGUI"
}
