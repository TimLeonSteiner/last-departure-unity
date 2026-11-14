Shader "Dustline/Spaceport Cel"
{
    Properties {
        _BaseColor("Surface", Color) = (.2,.3,.35,1)
        _Glow("Light emission", Range(0,4)) = 0
        _Ground("Wet ground", Range(0,1)) = 0
        _IndoorWarmth("Practical light fill", Range(0,1)) = 1
        _Metallic("Metal response", Range(0,1)) = .35
        _Smoothness("Surface smoothness", Range(0,1)) = .35
        _Wear("Hall surface decay", Range(0,1)) = 0
        _ExteriorWear("Storm damage", Range(0,1)) = 0
        _PaintWear("Faded road paint", Range(0,1)) = 0
        _Concrete("Exposed concrete", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Glow;
            half _Ground;
            half _IndoorWarmth;
            half _Metallic; half _Smoothness;
            half _Wear; half _Concrete;
            half _ExteriorWear; half _PaintWear;
            CBUFFER_END
            TEXTURE2D(_HallFloorBase); SAMPLER(sampler_HallFloorBase);
            TEXTURE2D(_HallFloorSurface); SAMPLER(sampler_HallFloorSurface);
            TEXTURE2D(_HallWallBase); SAMPLER(sampler_HallWallBase);
            TEXTURE2D(_HallWallSurface); SAMPLER(sampler_HallWallSurface);
            float _HallTextureReady;
            float4 _HallLeak0,_HallLeak1,_HallLeak2;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz); return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 p=i.positionWS;
                bool inside=max(abs(p.x),abs(p.z))<24.4 && p.y<8.3;
                half3 normal=normalize(i.normalWS);
                half3 view=normalize(_WorldSpaceCameraPos-p);
                float2 projection=abs(normal.y)>.65?p.xz:abs(normal.x)>.65?p.zy:p.xy;
                float pixelFootprint=length(fwidth(projection));
                float detailFade=1-smoothstep(.025,.12,pixelFootprint);
                float mottling=noise(projection*1.7)*.7+lerp(.5,noise(projection*9),detailFade)*.3;
                half3 color=_BaseColor.rgb*lerp(.84,1.08,mottling);
                // Edge wear and fine streaks are stable in world space, not screen-space noise.
                float stain=smoothstep(.64,.85,noise(projection*float2(7,.23)));
                color=lerp(color,color*(inside?half3(.66,.68,.70):half3(.65,.57,.45)),stain*.2);
                float wet=_Ground*smoothstep(.36,.64,noise(p.xz*.42));
                if(_Ground>.5) {
                    float2 grid=abs(frac(p.xz*.25+.5)-.5);
                    float joint=1-smoothstep(.005,.012,min(grid.x,grid.y));
                    color*=lerp(1,.48,joint);
                    color=lerp(color,color*.65+(inside?half3(.018,.017,.015):half3(.025,.055,.07)),wet*.7);
                }
                // World-space material textures: stable under movement, no repeated decal objects.
                if(inside && _Glow<.5) {
                    color*=half3(.82,.84,.86);
                    if(_Ground>.5 && normal.y>.6) {
                        float concrete=noise(p.xz*2.4)*.6+noise(p.xz*11)*.4;
                        color*=lerp(.6,1.25,concrete);
                        float2 slab=abs(frac(p.xz/4+.5)-.5)*4;
                        float seam=1-smoothstep(.012,.035,min(slab.x,slab.y));
                        float edgeWear=(1-smoothstep(.035,.11,min(slab.x,slab.y)))-seam;
                        color*=1-seam*.75;
                        color+=half3(.025,.032,.032)*edgeWear;
                        float crack=abs(noise(p.xz*.85+noise(p.xz*6)*.16)-.51);
                        float cracks=(1-smoothstep(.0015,.005,crack))*smoothstep(.51,.72,noise(p.xz*.23));
                        color*=1-cracks*.45;
                        float tyre=exp(-pow((abs(p.x)-3.8)*7,2))*smoothstep(.32,.6,noise(float2(p.z*.7,p.x*2)));
                        color*=1-tyre*.55;
                        // Broad irregular damp patches and scuffed access lanes.
                        float damp=smoothstep(.38,.7,noise(p.xz*.31));
                        color=lerp(color,color*half3(.60,.59,.56),damp*.65);
                        wet=max(wet,damp*.8);
                    }
                    if(abs(normal.y)<.35 && max(abs(p.x),abs(p.z))>22.7 && _Concrete<.5) {
                        float u=abs(normal.x)>.65?p.z:p.x;
                        float panelU=abs(frac(u/1.35+.5)-.5)*1.35;
                        float panelV=abs(frac(p.y/1.2+.5)-.5)*1.2;
                        float seam=1-smoothstep(.008,.025,min(panelU,panelV));
                        color*=1-seam*.65;
                        float rib=pow(.5+.5*cos(u*32),8)*detailFade;
                        color*=1-rib*.22;
                        float runoff=smoothstep(.56,.78,noise(float2(u*5,p.y*.28)));
                        color=lerp(color,color*half3(.57,.59,.60),runoff*.35);
                        float baseDirt=1-smoothstep(.05,1.25,p.y);
                        color*=1-baseDirt*.5;
                        float scratch=(1-smoothstep(.008,.025,abs(frac(u*3.7+p.y*.2)-.5)))*smoothstep(.64,.8,noise(projection*3));
                        color+=half3(.03,.04,.042)*scratch*detailFade;
                    }
                    if(normal.y<-.65 && p.y>7.9) {
                        float rib=pow(.5+.5*cos(p.x*11),6);
                        float2 panel=abs(frac(p.xz/float2(3,4)+.5)-.5);
                        float seam=1-smoothstep(.004,.014,min(panel.x,panel.y));
                        color*=lerp(.55,1.15,rib)*(1-seam*.7);
                        color*=lerp(.5,1,smoothstep(.2,.8,noise(p.xz*.55)));
                    }
                }
                if(!inside && _Glow<.01) {
                    // Broad weathering remains readable across the runway; fine grain fades with distance.
                    float runoff=smoothstep(.54,.79,noise(projection*float2(4.2,.19)));
                    color=lerp(color,color*half3(.59,.48,.36),runoff*.32);
                    if(_Ground>.5) {
                        float damp=smoothstep(.43,.72,noise(p.xz*.17));
                        color*=lerp(1,.55,damp);wet=max(wet,damp*.9);
                        float tyre=exp(-pow((abs(p.x)-2.1)*6,2))*smoothstep(.3,.7,noise(float2(p.z*.65,p.x*2)));
                        color*=1-tyre*.55;
                    }
                    float2 vehicle=p.xz-float2(13,102);
                    if(length(vehicle)<2.85 && p.y>3 && p.y<27.5) {
                        float circumference=atan2(vehicle.y,vehicle.x)*2.6;
                        float weld=1-smoothstep(.008,.03,abs(frac(circumference*1.05+.5)-.5));
                        color*=1-weld*.16;
                        color*=lerp(.72,1,smoothstep(3,6,p.y));
                    }
                }
                float grain=(noise(projection*65)-.5)*detailFade;
                if(!inside && _ExteriorWear>.01 && _Glow<.01) {
                    if(normal.y>.65 && p.y<.3) {
                        float patch=smoothstep(.58,.65,noise(p.xz*.39+3));
                        float patchEdge=smoothstep(.56,.59,noise(p.xz*.39+3))*(1-patch);
                        color*=lerp(1,.57,patch);
                        color*=1-patchEdge*.28;
                        float fracture=abs(noise(p.xz*.8+noise(p.xz*3.5)*.23)-.5);
                        float crack=(1-smoothstep(.003,.016,fracture))*smoothstep(.44,.63,noise(p.xz*.22));
                        color*=1-crack*.74;
                        float pool=smoothstep(.58,.71,noise(p.xz*.27));
                        wet=max(wet,pool*.95);color*=1-pool*.22;
                        float lostPaint=smoothstep(.40,.62,noise(p.xz*9)*.4+noise(p.xz*1.7)*.6);
                        color=lerp(color,half3(.038,.044,.045),lostPaint*_PaintWear*.85);
                    } else {
                        float seam=1-smoothstep(.025,.12,abs(frac(projection.x*.7+.5)-.5));
                        float rustRun=smoothstep(.57,.75,noise(projection*float2(5,.23)))*lerp(.16,1,seam);
                        color=lerp(color,color*.55+half3(.025,.010,.003),rustRun*.7);
                        color*=1-(1-smoothstep(0,1.5,p.y))*.2;
                    }
                }
                // Broad peeling islands, exposed aggregate and gravity streaks, in world metres.
                float decay = noise(projection*2.1)*.55 + noise(projection*6.7)*.3 + noise(projection*19)*.15;
                float seamAge=1-smoothstep(.03,.16,abs(frac(projection.x/1.35+.5)-.5));
                float peeling = smoothstep(.65,.78,decay)*lerp(.12,1,seamAge);
                float chippedEdge = smoothstep(.55,.58,decay)*(1-smoothstep(.58,.61,decay));
                float runoffWear = smoothstep(.51,.72,noise(projection*float2(9,.36)));
                if (_Wear>.01 && _Glow<.01) {
                    half3 exposed = lerp(color*.65+half3(.014,.015,.016),color*.55+half3(.028,.03,.032),_Concrete);
                    color=lerp(color,exposed,peeling*_Wear*.55);
                    color+=half3(.018,.016,.012)*chippedEdge*_Wear;
                    color*=1-runoffWear*_Wear*.25;
                    color*=lerp(.88,1.08,noise(projection*38))*_Wear+(1-_Wear);
                    float fracture=abs(noise(projection*1.8+noise(projection*7)*.25)-.5);
                    float crack=(1-smoothstep(.002,.013,fracture))*smoothstep(.48,.64,noise(projection*.6));
                    color*=1-crack*_Wear*lerp(.3,.75,_Concrete);
                }
                float brushed=noise(projection*float2(9,180));
                float materialMix=0,materialHeight=0,materialRoughness=.7,materialOcclusion=1;
                float floorMask=inside&&_Ground>.5&&normal.y>.65?1:0;
                float lowerWallMask=inside&&abs(normal.y)<.35&&max(abs(p.x),abs(p.z))>22.7
                    ?1-smoothstep(2.4,3.4,p.y):0;
                if(_HallTextureReady>.5&&_Glow<.01&&(floorMask>0||lowerWallMask>0))
                {
                    // Two metre tiles for aggregate; wall paint at a finer one metre scale.
                    float concreteSurface=max(floorMask,lowerWallMask*_Concrete);
                    float2 uv=projection*(concreteSurface>.5?.5:1);
                    half3 baseMap=concreteSurface>.5?SAMPLE_TEXTURE2D(_HallFloorBase,sampler_HallFloorBase,uv).rgb
                        :SAMPLE_TEXTURE2D(_HallWallBase,sampler_HallWallBase,uv).rgb;
                    half3 packed=concreteSurface>.5?SAMPLE_TEXTURE2D(_HallFloorSurface,sampler_HallFloorSurface,uv).rgb
                        :SAMPLE_TEXTURE2D(_HallWallSurface,sampler_HallWallSurface,uv).rgb;
                    materialMix=max(floorMask,lowerWallMask);
                    color*=lerp(half3(1,1,1),clamp(baseMap*1.9,.55,1.35),materialMix);
                    materialHeight=(packed.g-.5)*lerp(.0015,.006,concreteSurface)*materialMix;
                    materialRoughness=packed.r;materialOcclusion=lerp(1,packed.b,materialMix);
                    if(floorMask>.5)
                    {
                        // Water collects below the three real roof breaches, not uniformly indoors.
                        float leak=min(length(p.xz-_HallLeak0.xy),min(length(p.xz-_HallLeak1.xy),length(p.xz-_HallLeak2.xy)));
                        float basin=1-smoothstep(1.1,3.6,leak+noise(p.xz*1.3)*.8);
                        float puddle=basin*smoothstep(.43,.68,noise(p.xz*.8));
                        float perimeter=smoothstep(20.8,23.7,max(abs(p.x),abs(p.z)));
                        wet=max(puddle,(1-smoothstep(2,5,leak))*.25);
                        color*=1-wet*.42-perimeter*.30;
                        float tide=(1-smoothstep(.08,.38,abs(leak-2.8+noise(p.xz*2)*.45)));
                        color=lerp(color,color*half3(.42,.28,.15),tide*.72);
                        // Repaired slab sections have a quieter grain and rough, dry edges.
                        float2 tile=floor((p.xz+2)/4);
                        float2 local=abs(frac((p.xz+2)/4)-.5)*4;
                        float slabAge=hash(tile+17);
                        color*=lerp(.65,1.24,slabAge);
                        // Offset, ragged repairs cross the slab grid rather than filling identical squares.
                        float2 repairUV=p.xz+float2(noise(p.xz*.7),noise(p.zx*.9))* .7;
                        float patchA=length((repairUV-float2(5.8,-10.7))/float2(2.1,1.1));
                        float patchB=length((repairUV-float2(-6.4,7.2))/float2(1.2,3));
                        float patchC=length((repairUV-float2(15,3))/float2(2.4,1.4));
                        float patchDistance=min(patchA,min(patchB,patchC));
                        float repair=1-smoothstep(.83,1,patchDistance);
                        float repairEdge=(1-smoothstep(.99,1.08,patchDistance))-repair;
                        color=lerp(color,color*half3(.47,.49,.48),repair*.85);
                        color*=1-repairEdge*.48;
                        materialRoughness=lerp(materialRoughness,.91,repair*.7);
                        materialHeight*=lerp(1,.45,repair);
                        // Broken paired wheel marks follow the central walking corridor.
                        float lane=exp(-pow((abs(p.x)-3.8)*5,2));
                        float tread=lane*smoothstep(.4,.65,noise(float2(p.z*2,p.x*5)));
                        color*=1-tread*.48;
                        materialRoughness=lerp(materialRoughness,.64,tread*.5);
                        // Old equipment leaks are dark glossy islands beside the racks.
                        float oilDistance=min(length((p.xz-float2(8,-12.8))/float2(1.4,.8)),
                            min(length((p.xz-float2(-8,9))/float2(2,1.1)),length((p.xz-float2(16,-4))/float2(1.3,2.2))));
                        float oil=1-smoothstep(.65,1.05,oilDistance+noise(p.xz*4)*.35);
                        color=lerp(color,half3(.012,.010,.008),oil*.85);
                        materialRoughness=lerp(materialRoughness,.22,oil);
                        // Broad dust dragged from the loading doors, interrupted by traffic.
                        float drag=smoothstep(.46,.72,noise(float2(p.x*.6,p.z*.11)+noise(p.xz*.8)*.25));
                        float dust=drag*smoothstep(.3,.65,noise(p.xz*.23+8));
                        color=lerp(color,color*half3(1.5,1.23,.78)+half3(.05,.035,.015),dust*.85);
                        float groundGrime=smoothstep(.42,.7,noise(p.xz*.38+3)*.65+noise(p.xz*2.3)*.35);
                        color=lerp(color,color*half3(.30,.25,.18),groundGrime*.7);
                        materialRoughness=lerp(materialRoughness,.95,dust*.6);
                        float edgeChips=(1-smoothstep(.015,.13,(2-max(local.x,local.y))))
                            *smoothstep(.54,.75,noise(p.xz*19));
                        color*=1-edgeChips*.55;
                        materialHeight-=edgeChips*.003;
                    }
                    else
                    {
                        float u=abs(normal.x)>.65?p.z:p.x;
                        float footDirt=(1-smoothstep(.12,1.1,p.y))*smoothstep(.25,.7,noise(float2(u*2,p.y*3)));
                        float joint=1-smoothstep(.015,.09,abs(frac(u/1.35+.5)-.5));
                        float rust=joint*smoothstep(.48,.75,noise(float2(u*8,p.y*.45)))*lowerWallMask*(1-_Concrete);
                        color=lerp(color,color*half3(.62,.43,.24),rust*.65);
                        float tide=1-smoothstep(.025,.1,abs(p.y-(.48+noise(float2(u*.45,0))*.26)));
                        float bloom=smoothstep(.4,.73,noise(float2(u*1.6,p.y*2.2)))*(1-smoothstep(.45,1.5,p.y));
                        color*=1-footDirt*.48-bloom*.22;
                        color=lerp(color,color*half3(.55,.43,.29),tide*bloom*.65);
                        float flake=smoothstep(.54,.68,noise(projection*5)*.45+noise(projection*1.1)*.55);
                        color=lerp(color,color*half3(1.3,1.22,1.08),flake*lowerWallMask*.4);
                        materialRoughness=saturate(materialRoughness+footDirt*.12+rust*.15);
                        wet=0;
                    }
                }
                // The heart occupies a purpose-built service bay within the concrete hall.
                // World-space panels are flush with the original floor and add no geometry.
                float serviceMetal=0;
                if(floorMask>.5 && p.y<.005 && _Glow<.01)
                {
                    float2 q=abs(p.xz);
                    float octagon=max(max(q.x,q.y),(q.x+q.y)*.70710678);
                    float aa=max(.008,pixelFootprint*.7);
                    float bay=1-smoothstep(6.85-aa,6.85+aa,octagon);
                    float along=max(q.x,q.y),across=min(q.x,q.y);
                    float route=(1-smoothstep(2.9-aa,2.9+aa,across))*smoothstep(6.7,7,along);
                    float metalMask=max(bay,route*.8);
                    float2 cell=floor((p.xz+1)/2), panel=abs(frac((p.xz+1)/2)-.5)*2;
                    float joint=1-smoothstep(.014,.014+aa,1-max(panel.x,panel.y));
                    float lip=(1-smoothstep(.035,.035+aa,1-max(panel.x,panel.y)))-joint;
                    float variation=hash(cell+31);
                    half3 plate=lerp(half3(.105,.125,.13),half3(.19,.21,.21),variation);
                    plate*=lerp(.76,1.13,noise(p.xz*3.8));
                    float grime=smoothstep(.45,.8,noise(p.xz*.65+9));
                    plate=lerp(plate,half3(.065,.047,.031),grime*.35);
                    plate*=1-joint*.78;plate+=lip*.045;
                    float boltDistance=length(panel-float2(.86,.86));
                    float socket=1-smoothstep(.055,.055+aa,boltDistance);
                    float bolt=1-smoothstep(.027,.027+aa,boltDistance);
                    plate=lerp(plate,half3(.023,.027,.027),socket);
                    plate+=bolt*.09;
                    color=lerp(color,plate,metalMask);
                    serviceMetal=metalMask*.62;
                    materialHeight=lerp(materialHeight,-joint*.003,metalMask);
                    materialRoughness=lerp(materialRoughness,.59+grime*.21,metalMask);
                    // Paired covered utility trenches meet the existing heart connections.
                    float trench=(1-smoothstep(.30,.30+aa,abs(across-2.6)))*smoothstep(4.1,4.5,along);
                    float grate=1-smoothstep(.027,.027+aa,abs(frac(along/.16)-.5)*.16);
                    float rail=smoothstep(.255-aa,.255,abs(across-2.6));
                    half3 grille=lerp(half3(.018,.023,.025),half3(.17,.19,.19),max(grate,rail));
                    color=lerp(color,grille,trench);
                    materialHeight-=trench*(1-max(grate,rail))*.004;
                    serviceMetal=max(serviceMetal,trench*.7);
                    // Traffic burnishes the four approaches; grime stays in seams and drains.
                    float traffic=exp(-pow(across/1.4,4))*smoothstep(4.5,5.5,along)*(1-smoothstep(18,22,along));
                    float abrasion=smoothstep(.47,.72,noise(float2(along*.7,across*16)))*traffic;
                    color=lerp(color,color*1.23+half3(.012,.011,.009),abrasion*.45);
                    materialRoughness=lerp(materialRoughness,.48,abrasion*.35);
                    float drainRust=exp(-pow((abs(across-2.6)-.33)*9,2))*smoothstep(4.5,5.2,along);
                    drainRust*=smoothstep(.35,.74,noise(p.xz*3+21));
                    color=lerp(color,half3(.095,.046,.019),drainRust*.48);
                    float trappedDirt=joint*metalMask*smoothstep(.30,.7,noise(p.xz*2.7));
                    color=lerp(color,half3(.028,.021,.014),trappedDirt*.6);

                    // Broken ochre perimeter stripes leave the four approach lanes open.
                    float border=(1-smoothstep(.16,.16+aa,abs(octagon-7.12)))*smoothstep(3.05,3.2,across);
                    float stripe=smoothstep(.46,.54,frac((p.x+p.z)*1.8));
                    float paintWear=smoothstep(.29,.61,noise(p.xz*12)*.35+noise(p.xz*2.1)*.65);
                    half3 warning=lerp(half3(.028,.031,.028),half3(.40,.28,.075),stripe);
                    color=lerp(color,warning,border*paintWear*.9);
                    // Narrow faded lane edges carry the maintenance layout toward each gate.
                    float laneEdge=(1-smoothstep(.045,.045+aa,abs(across-3.12)))*smoothstep(7.3,7.6,along);
                    float dash=smoothstep(.12,.16,frac(along/1.8));
                    color=lerp(color,half3(.32,.28,.16),laneEdge*dash*paintWear*.65);
                    wet*=1-metalMask*.75;
                    materialMix=1;
                }

                // Abraded safety paint exposes concrete, while retaining readable route markings.
                if(inside&&p.y<.16&&normal.y>.65&&_Glow<.01&&_BaseColor.r>_BaseColor.g*1.15&&_BaseColor.g>_BaseColor.b*1.4)
                {
                    float loss=smoothstep(.46,.64,noise(p.xz*13)*.45+noise(p.xz*2.8)*.55);
                    float scrape=smoothstep(.62,.8,noise(float2(p.x*38,p.z*2)));
                    color=lerp(color,half3(.075,.069,.055),saturate(loss*.85+scrape*.55));
                }
                float scuff=(1-smoothstep(.006,.025,abs(frac(projection.y*53+projection.x*.6)-.5)))
                    *smoothstep(.69,.85,noise(projection*float2(3,19)))*detailFade;
                if(_Glow<.01&&_Ground<.5)color+=half3(.035,.039,.042)*scuff;
                // Surface-gradient bump preserves physical light response on every wall orientation.
                float height=grain*.0007+(_Ground>.5?noise(projection*11)*.0015:brushed*.00015);
                height+=_Wear*(peeling*.0012+noise(projection*38)*.00035+chippedEdge*.0006);
                height+=materialHeight;
                height*=1-wet*.85;
                float3 dx=ddx(p),dy=ddy(p),rx=cross(dy,normal),ry=cross(normal,dx);
                float determinant=dot(dx,rx);
                normal=normalize(abs(determinant)*normal-sign(determinant)*(ddx(height)*rx+ddy(height)*ry)+normal*1e-10);
                InputData inputData=(InputData)0;
                inputData.positionWS=p; inputData.normalWS=normal; inputData.viewDirectionWS=view;
                inputData.shadowCoord=TransformWorldToShadowCoord(p);
                // Only scene/probe illumination belongs in GI. A constant fill makes
                // ceilings stay bright even when fixtures and the torch are off.
                inputData.bakedGI=SampleSH(normal);
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=color;surface.alpha=1;surface.metallic=_Ground>.5?0:_Metallic;
                surface.smoothness=saturate(_Smoothness+wet*.3-grain*.1-scuff*.14);
                surface.smoothness*=1-_Wear*peeling*.7;
                surface.metallic*=1-_Wear*peeling;
                surface.smoothness=lerp(surface.smoothness,lerp(1-materialRoughness,.88,wet),materialMix);
                surface.metallic=lerp(surface.metallic,serviceMetal,materialMix);
                surface.normalTS=half3(0,0,1);surface.occlusion=materialOcclusion;
                surface.emission=_Glow>.01?_BaseColor.rgb*_Glow:half3(0,0,0);
                color=UniversalFragmentPBR(inputData,surface).rgb;
                // Practical illumination and wet specular highlights come from the actual lights.
                float distance=length(_WorldSpaceCameraPos-p);
                float haze=(1-exp(-distance*unity_FogParams.y*.693147))*(.45+.25*exp(-max(p.y,0)*.12));
                color=lerp(color,unity_FogColor.rgb,inside?haze*.22:haze);
                return half4(color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            struct DepthInput {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct DepthOutput {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;};
            DepthOutput DepthVert(DepthInput v)
            {
                DepthOutput o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(v.normalOS);return o;
            }
            half4 DepthFrag(DepthOutput i):SV_Target
            {
                float3 n=normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n)*.5+.5)),0);
                #else
                return half4(n,0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor; half _Glow; half _Ground; half _IndoorWarmth; half _Metallic; half _Smoothness;
            half _Wear; half _Concrete;
            half _ExteriorWear; half _PaintWear;
            CBUFFER_END
            float3 _LightDirection; float3 _LightPosition;
            float4 ShadowVert(float4 positionOS:POSITION,float3 normalOS:NORMAL):SV_POSITION
            {
                float3 p=TransformObjectToWorld(positionOS.xyz);
                float3 n=TransformObjectToWorldNormal(normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction=normalize(_LightPosition-p);
                #else
                float3 direction=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,direction));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }
            half4 ShadowFrag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}

