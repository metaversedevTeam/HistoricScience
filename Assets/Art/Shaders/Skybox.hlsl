// 스타일라이즈드 하늘(하늘 그라데이션, 태양, 달, 별, 구름, 시간 전환)을 계산하는 셰이더 그래프 커스텀 함수. Skybox.shadergraph의 Custom Function 노드가 호출한다.
// 주의: Unity는 패스가 정확히 6개인 스카이박스 셰이더를 6면 큐브 스카이박스로 보고 면마다 다른 패스로 그린다. 그래서 그래프의 Cast Shadows를 켜 ShadowCaster 패스로 7개를 만든다(그림자에는 영향 없음).
// 태양 방향 계산식은 SkyboxTimeOfDay.cs의 GetSunDirection과 반드시 같아야 디렉셔널 라이트와 하늘의 태양 위치가 일치한다.
#ifndef HISTORICSCIENCE_SKYBOX_INCLUDED
#define HISTORICSCIENCE_SKYBOX_INCLUDED

#define SKY_TWO_PI 6.28318530718

// 2D 좌표를 0~1 사이 의사 난수로 바꾼다.
float SkyHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// 3D 격자 좌표를 0~1 사이 의사 난수 3개로 바꾼다.
float3 SkyHash33(float3 p)
{
    p = frac(p * float3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return frac((p.xxy + p.yxx) * p.zyx);
}

// 격자 꼭짓점 난수를 부드럽게 보간한 2D 값 노이즈(0~1)를 반환한다.
float SkyValueNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = SkyHash21(cell);
    float b = SkyHash21(cell + float2(1.0, 0.0));
    float c = SkyHash21(cell + float2(0.0, 1.0));
    float d = SkyHash21(cell + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 값 노이즈 네 옥타브를 겹친 프랙탈 노이즈(약 0~1)를 반환한다.
float SkyFbm(float2 p)
{
    float sum = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 4; i++)
    {
        sum += SkyValueNoise(p) * amplitude;
        p = p * 2.03 + float2(17.1, 9.7);
        amplitude *= 0.5;
    }
    return sum / 0.9375;
}

// 축 axis를 중심으로 벡터 v를 angle(라디안)만큼 회전한다(로드리게스 회전).
float3 SkyRotateAroundAxis(float3 v, float3 axis, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}

// Y축 기준 yaw 회전과 X축 기준 tilt 회전을 Unity의 Quaternion.Euler(tilt, yaw, 0)과 같은 순서로 적용한다.
float3 SkyApplyOrbitRotation(float3 v, float yawRad, float tiltRad)
{
    float3 tilted = float3(v.x, v.y * cos(tiltRad) - v.z * sin(tiltRad), v.y * sin(tiltRad) + v.z * cos(tiltRad));
    return float3(tilted.x * cos(yawRad) + tilted.z * sin(yawRad), tilted.y, -tilted.x * sin(yawRad) + tilted.z * cos(yawRad));
}

// 하루 진행도(0=자정, 0.25=일출, 0.5=정오, 0.75=일몰)로 태양 방향을 구한다.
float3 SkySunDirection(float timeOfDay, float yawRad, float tiltRad)
{
    float angle = (timeOfDay - 0.25) * SKY_TWO_PI;
    return normalize(SkyApplyOrbitRotation(float3(cos(angle), sin(angle), 0.0), yawRad, tiltRad));
}

// 시점 방향과 태양 높이로 하늘 바탕색(낮·노을·밤 그라데이션)을 만든다.
float3 SkyGradient(float3 dir, float3 sunDir, float dayAmount, float twilight,
    float3 dayTop, float3 dayHorizon, float3 sunsetColor, float3 nightTop, float3 nightHorizon)
{
    float3 top = lerp(nightTop, dayTop, dayAmount);
    float3 horizon = lerp(nightHorizon, dayHorizon, dayAmount);

    float height = saturate(dir.y);
    float3 sky = lerp(horizon, top, pow(height, 0.5));

    // 노을은 지평선 근처, 태양을 바라보는 쪽에서 가장 짙다.
    float horizonBand = 1.0 - saturate(abs(dir.y) * 2.5);
    float towardSun = pow(saturate(dot(dir, sunDir) * 0.5 + 0.5), 3.0);
    sky = lerp(sky, sunsetColor, twilight * horizonBand * (0.3 + 0.7 * towardSun));

    // 지평선 아래는 지평선 색을 조금 어둡게 이어 붙인다.
    float below = saturate(-dir.y * 4.0);
    return lerp(sky, horizon * 0.75, below);
}

// 하늘과 함께 회전하는 3D 격자에서 반짝이는 별 밝기를 구한다.
float SkyStars(float3 starDir, float density, float time, float twinkleSpeed)
{
    float3 p = starDir * density;
    float3 cell = floor(p);
    float3 rnd = SkyHash33(cell);

    // 격자 칸의 일부에만 별을 둔다.
    float hasStar = step(0.72, rnd.x);
    float3 starPos = cell + 0.25 + rnd.yzx * 0.5;
    float dist = length(p - starPos);
    float size = lerp(0.05, 0.12, rnd.y);
    float star = saturate(1.0 - dist / size);
    star *= star;

    float twinkle = 0.65 + 0.35 * sin(time * twinkleSpeed * (0.6 + rnd.z) + rnd.y * SKY_TWO_PI);
    return star * hasStar * twinkle * lerp(0.4, 1.0, rnd.z);
}

// 달 원반(위상 그림자·얼룩 포함)과 달무리를 계산한다. disc는 달이 하늘을 가리는 정도, glow는 달무리 밝기다.
void SkyMoon(float3 dir, float3 moonDir, float size, float phase, out float disc, out float lit, out float glow)
{
    float3 up = abs(moonDir.y) > 0.99 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
    float3 right = normalize(cross(up, moonDir));
    float3 localUp = cross(moonDir, right);

    float3 offset = dir - moonDir;
    float2 local = float2(dot(offset, right), dot(offset, localUp)) / max(size, 1e-4);
    float facing = step(0.0, dot(dir, moonDir));

    float r = length(local);
    disc = (1.0 - smoothstep(0.92, 1.0, r)) * facing;

    // 위상: 원반을 반구로 보고 옆에서 비추는 빛으로 명암 경계를 만든다. 0이면 보름달, 0.5면 반달, 1이면 그믐(완전히 어두움).
    float3 normal = float3(local, sqrt(saturate(1.0 - r * r)));
    float phaseAngle = saturate(phase) * PI;
    float3 lightDir = float3(sin(phaseAngle), 0.0, cos(phaseAngle));
    float shade = smoothstep(-0.04, 0.06, dot(normal, lightDir));
    float spots = lerp(0.78, 1.0, SkyValueNoise(local * 2.6 + 3.1));
    lit = disc * shade * spots;

    glow = pow(saturate(1.0 - length(offset) * 1.6), 6.0) * facing;
}

// 구름 덮임 정도(alpha)와 색을 계산한다. 하늘을 평면에 투영해 스크롤하는 스타일라이즈드 구름이다.
void SkyClouds(float3 dir, float3 sunDir, float time, float scale, float speed, float coverage, float softness,
    float3 litColor, float3 shadeColor, out float alpha, out float3 color)
{
    float2 uv = dir.xz / (max(dir.y, 0.0) + 0.18) * scale;
    uv += float2(1.0, 0.35) * time * speed;

    float n = SkyFbm(uv);
    float edge = 1.0 - coverage;
    alpha = smoothstep(edge, edge + max(softness, 1e-3), n);
    alpha *= smoothstep(0.0, 0.2, dir.y);

    // 태양 쪽으로 살짝 옮긴 샘플과 비교해 해를 향한 면은 밝게, 반대면은 어둡게 칠한다.
    float nTowardSun = SkyFbm(uv + normalize(sunDir.xz + 1e-4) * 0.12);
    float lightAmount = saturate(0.55 + (n - nTowardSun) * 5.0);
    color = lerp(shadeColor, litColor, lightAmount);
}

// 셰이더 그래프 Custom Function 노드의 진입점. 방향별 하늘 색을 계산한다.
void SkyboxSky_float(
    float3 Direction,
    float TimeOfDay,
    float SunOrbitYaw,
    float SunOrbitTilt,
    float4 DayTopColor,
    float4 DayHorizonColor,
    float4 SunsetColor,
    float4 NightTopColor,
    float4 NightHorizonColor,
    float4 SunColor,
    float SunSize,
    float SunGlow,
    float4 MoonColor,
    float MoonSize,
    float MoonPhase,
    float StarDensity,
    float StarBrightness,
    float StarTwinkleSpeed,
    float4 CloudColor,
    float4 CloudShadeColor,
    float CloudCoverage,
    float CloudSoftness,
    float CloudScale,
    float CloudSpeed,
    out float3 Color)
{
    // 스카이박스 메시의 월드 위치에서 카메라 위치를 빼 시점 방향을 얻는다.
    float3 dir = normalize(Direction - _WorldSpaceCameraPos);
    float time = _TimeParameters.x;
    float yawRad = radians(SunOrbitYaw);
    float tiltRad = radians(SunOrbitTilt);

    float3 sunDir = SkySunDirection(TimeOfDay, yawRad, tiltRad);
    float3 moonDir = -sunDir;

    // 시간대 가중치: 낮(태양이 떠 있음), 노을(태양이 지평선 근처), 밤(낮의 반대).
    float dayAmount = smoothstep(-0.15, 0.2, sunDir.y);
    float twilight = 1.0 - smoothstep(0.0, 0.3, abs(sunDir.y + 0.03));
    float nightAmount = 1.0 - smoothstep(-0.2, 0.05, sunDir.y);

    float3 sky = SkyGradient(dir, sunDir, dayAmount, twilight,
        DayTopColor.rgb, DayHorizonColor.rgb, SunsetColor.rgb, NightTopColor.rgb, NightHorizonColor.rgb);

    // 구름: 낮에는 지정 색, 노을엔 노을빛, 밤에는 밤하늘 색으로 어둡게 물든다.
    float3 cloudLit = lerp(NightTopColor.rgb * 2.2 + MoonColor.rgb * 0.08, CloudColor.rgb, dayAmount);
    cloudLit = lerp(cloudLit, SunsetColor.rgb * 1.1, twilight * 0.6);
    float3 cloudShade = lerp(NightTopColor.rgb * 1.2, CloudShadeColor.rgb, dayAmount);
    cloudShade = lerp(cloudShade, SunsetColor.rgb * 0.55, twilight * 0.4);
    float cloudAlpha;
    float3 cloudColor;
    SkyClouds(dir, sunDir, time, CloudScale, CloudSpeed, CloudCoverage, CloudSoftness, cloudLit, cloudShade, cloudAlpha, cloudColor);

    float aboveHorizon = smoothstep(-0.02, 0.03, dir.y);

    // 별(밤 한정): 태양 궤도축을 중심으로 하늘과 함께 돈다.
    float3 orbitAxis = normalize(SkyApplyOrbitRotation(float3(0.0, 0.0, 1.0), yawRad, tiltRad));
    float3 starDir = SkyRotateAroundAxis(dir, orbitAxis, -TimeOfDay * SKY_TWO_PI);
    float stars = SkyStars(starDir, StarDensity, time, StarTwinkleSpeed);
    stars *= StarBrightness * nightAmount * nightAmount * smoothstep(0.0, 0.25, dir.y);

    // 달(밤 한정)
    float moonVisible = (1.0 - dayAmount) * smoothstep(-0.05, 0.08, moonDir.y) * aboveHorizon;
    float moonDisc, moonLit, moonGlow;
    SkyMoon(dir, moonDir, MoonSize, MoonPhase, moonDisc, moonLit, moonGlow);

    // 태양(낮 한정)
    float sunVisible = smoothstep(-0.04, 0.04, sunDir.y) * aboveHorizon;
    float sunDist = length(dir - sunDir);
    float sunDisc = 1.0 - smoothstep(SunSize * 0.9, SunSize, sunDist);
    float sunHalo = pow(saturate(1.0 - sunDist * 0.9), 8.0) * SunGlow;

    float3 color = sky;
    color += stars * (1.0 - moonDisc * moonVisible);
    color += MoonColor.rgb * moonGlow * 0.12 * moonVisible;
    // 달의 어두운 면은 뒤의 하늘보다 조금 어둡게, 밝은 면은 달빛 색으로 칠한다.
    color = lerp(color, color * 0.55 + MoonColor.rgb * moonLit, moonDisc * moonVisible);
    color += SunColor.rgb * sunHalo * sunVisible;
    color = lerp(color, SunColor.rgb, sunDisc * sunVisible);

    // 구름이 해·달·별을 가린다. 해 근처 구름 가장자리는 햇빛을 받아 밝아진다.
    cloudColor += SunColor.rgb * sunHalo * sunVisible * 0.35;
    color = lerp(color, cloudColor, cloudAlpha);

    Color = max(color, 0.0);
}

#endif
