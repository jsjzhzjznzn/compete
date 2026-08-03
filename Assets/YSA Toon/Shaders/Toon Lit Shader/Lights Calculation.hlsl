void MainLightColor_float(out float3 Color){
    #ifdef SHADERGRAPH_PREVIEW
        Color = float3(1, 1, 1);
    #else
        Color = GetMainLight().color;
    #endif
}

void MainLightDirection_float(out float3 Direction){
    #ifdef SHADERGRAPH_PREVIEW
        Direction = float3(1, 1, 1);
    #else
        Direction = GetMainLight().direction;
    #endif
}

void MainLightCalculation_float(float3 Position, float3 Normal, float lightMidPoint, float lightSmoothness,
    out float Output)
{
    #ifdef SHADERGRAPH_PREVIEW
        Output = 1;
    #else
        Light light = GetMainLight();

        float diffuse = dot(light.direction, Normal);
        diffuse = smoothstep(lightMidPoint, lightMidPoint+lightSmoothness, diffuse);

        float attenuation = light.distanceAttenuation;

        Output = diffuse * attenuation;
    #endif
}

void MainShadowsCalculation_float(float3 Position, bool enabled, float occlusion, float shadowSmoothness, float shadowPower, out float Output){

    #ifdef SHADERGRAPH_PREVIEW
        Output = 1;
    #else
        if(enabled){
            #if SHADOWS_SCREEN
			    float4 clipPos = TransformWorldToHClip(Position);
			    float4 shadowCoord = ComputeScreenPos(clipPos);
		    #else
			    float4 shadowCoord = TransformWorldToShadowCoord(Position);
		    #endif

            float shadow = GetMainLight(shadowCoord).shadowAttenuation;
            shadow = smoothstep(0, shadowSmoothness, shadow);
            shadow = max(shadow, 1-shadowPower);

            Output = shadow * occlusion;
        }else{
            Output = occlusion;
        }
    #endif
}

void AdditionalShadowsCalculation_float(float3 Position, bool enabled, float shadowSmoothness, float shadowPower, out float Output){
    Output = 1;

    #ifdef SHADERGRAPH_PREVIEW
        Output = 1;
    #else
        if(enabled){
            int pixelLightCount = GetAdditionalLightsCount();

		    for(int i = 0; i < pixelLightCount; i++){
			    Light light = GetAdditionalLight(i, Position, 1);

                float shadow = light.shadowAttenuation;

                shadow = smoothstep(0, shadowSmoothness, shadow);
                shadow = max(shadow, 1-shadowPower);

                Output *= shadow;
		    }
        }else{
            Output = 1;
        }
    #endif
}

void AdditionalLightsCalculation_float(float3 Position, float3 Normal,
    float lightMidPoint, float lightSmoothness, out float3 Color)
{
    #ifdef SHADERGRAPH_PREVIEW
        Color = float3(1,1,1);
    #else
        Color = float3(0,0,0);

        int pixelLightCount = GetAdditionalLightsCount();

		for(int i = 0; i < pixelLightCount; i++){
			Light light = GetAdditionalLight(i, Position, 1);

            float diffuse = dot(light.direction, Normal);
            diffuse = smoothstep(lightMidPoint, lightMidPoint+lightSmoothness, diffuse);
            diffuse = saturate(diffuse);

            float attenuation = light.distanceAttenuation;

			Color += diffuse * attenuation * light.color;
		}
    #endif
}

void SpecularMainLightingCalculation_float(float3 Normal, float3 View, float Map, float MidPoint, float Smoothness, float4 Multiplier, out float3 Color){

    #ifdef SHADERGRAPH_PREVIEW
        Color = float3(1,1,1);
    #else
        Light light = GetMainLight();

        float3 h = normalize(View + light.direction) + Map;
	    float specular = dot(Normal, h);

        specular = smoothstep(MidPoint, MidPoint+Smoothness, specular);

        Color = saturate(specular) * Multiplier.rgb * Multiplier.a * light.distanceAttenuation * light.color;
    #endif
}

void SpecularAdditionalLightingCalculation_float(float3 Position, float3 Normal, float3 View, float Map, float MidPoint, float Smoothness, float4 Multiplier, out float3 Color){

    #ifdef SHADERGRAPH_PREVIEW
        Color = float3(1,1,1);
    #else

        int pixelLightCount = GetAdditionalLightsCount();

		for(int i = 0; i < pixelLightCount; i++){
			Light light = GetAdditionalLight(i, Position, 1);

            float3 h = SafeNormalize(View + light.direction) + Map;
	        float specular = dot(Normal, h);

            specular = smoothstep(MidPoint, MidPoint+Smoothness, specular);

            Color = saturate(specular) * Multiplier.rgb * Multiplier.a * light.distanceAttenuation * light.color;
		}
        
    #endif
}

void RimLightingCalculation_float(float3 Normal, float3 View, float3 RimColor, float RimOpacity, float RimMidPoint, float RimSmoothness, float DynamicRemap, out float3 Color){

    #ifdef SHADERGRAPH_PREVIEW
        Color = float3(1,1,1);
    #else

        float rimDot = 1 - dot(View, Normal);

        float diffuse = dot(GetMainLight().direction, Normal);

        float factor = lerp(1, diffuse, DynamicRemap);

        float rim = smoothstep(lerp(1, RimMidPoint, factor), lerp(1, RimMidPoint, factor)+RimSmoothness, rimDot);

        Color = saturate(rim) * RimColor * RimOpacity;
        
    #endif
}

void FaceNormalsGenerator_float(float3 Position, float3 Normal, 
    bool PreviewX, float XOffset, float XPosMultiplier, float XModifier,
    bool PreviewY, float YOffset, float YPosMultiplier, float YModifier,
    bool PreviewZ, float ZOffset, float ZPosMultiplier, float ZModifier,
    out float3 Fixed){

    #ifdef SHADERGRAPH_PREVIEW
        Fixed = float3(1,1,1);
    #else

        Position += float3(XOffset, YOffset, ZOffset);
        Position *= float3(XPosMultiplier, YPosMultiplier, ZPosMultiplier);

        if(PreviewX){
            float r = Position.x;
            float g = Position.x;
            float b = Position.x;

            if(Position.x < -1 || Position.x > 1){
                r = 1;
                g = 0;
                b = 0;
            }

            Fixed = float3(r, g, b);
        }

        if(PreviewY){
            float r = Position.y;
            float g = Position.y;
            float b = Position.y;

            if(Position.y < -1 || Position.y > 1){
                r = 1;
                g = 0;
                b = 0;
            }

            Fixed = float3(r, g, b);
        }

        if(PreviewZ){
            float r = Position.z;
            float g = Position.z;
            float b = Position.z;

            if(Position.z < -1 || Position.z > 1){
                r = 1;
                g = 0;
                b = 0;
            }

            Fixed = float3(r, g, b);
        }

        
        float FixedX = lerp(Normal.x, Position.x, XModifier);
        float FixedY = lerp(Normal.y, Position.y, YModifier);
        float FixedZ = lerp(Normal.z, Position.z, ZModifier);

        if(!PreviewX && !PreviewY && !PreviewZ){
            Fixed = float3(FixedX, FixedY, FixedZ);
        }
        
    #endif

}