// 카메라로부터의 실제 거리(반경)로 거리 안개 비율을 구하는 공용 함수. RadialFog.shader와 반투명 셰이더(Water)가 함께 쓴다.
// URP 내장 안개는 카메라 정면 방향 깊이로 재서 화면 가장자리일수록 안개가 옅어지므로, 내장 안개 대신 이 식을 쓴다.
// 값은 DistanceFogController가 전역 셰이더 값으로 넣는다.
#ifndef HISTORICSCIENCE_RADIAL_FOG_INCLUDED
#define HISTORICSCIENCE_RADIAL_FOG_INCLUDED

// x: 시작 거리, y: 끝 거리, z: 1 / (끝 거리 - 시작 거리), w: 켜져 있으면 1, 꺼져 있으면 0
float4 _RadialFogParams;
// 먼 물체와 섞이는 안개 색
float4 _RadialFogColor;

// 월드 위치가 카메라에서 떨어진 실제 거리로 안개 비율(0: 원래 색 ~ 1: 안개 색)을 구한다.
float RadialFogFactor(float3 positionWS)
{
    float distanceToCamera = distance(positionWS, _WorldSpaceCameraPos);
    return saturate((distanceToCamera - _RadialFogParams.x) * _RadialFogParams.z) * _RadialFogParams.w;
}

// 이미 조명이 적용된 최종 색에 반경 안개를 섞는다.
float3 RadialFogMix(float3 color, float3 positionWS)
{
    return lerp(color, _RadialFogColor.rgb, RadialFogFactor(positionWS));
}

#endif
