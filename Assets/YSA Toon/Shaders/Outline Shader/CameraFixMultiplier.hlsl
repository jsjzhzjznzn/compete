float GetCameraFOV()
{
    float t = unity_CameraProjection._m11;
    float Rad2Deg = 180 / 3.1415;
    float fov = atan(1.0f / t) * 2.0 * Rad2Deg;
    return fov;
}

void GetCameraFixMultiplier_float(float positionVS_Z, out float fix)
{
    float cameraMulFix;
    if(unity_OrthoParams.w == 0)
    {    
        cameraMulFix = saturate(abs(positionVS_Z));
        cameraMulFix *= GetCameraFOV();       
    }
    else
    {
        float orthoSize = saturate(abs(unity_OrthoParams.y));
        cameraMulFix = orthoSize * 125;
    }

    fix = cameraMulFix * 0.0001;
}