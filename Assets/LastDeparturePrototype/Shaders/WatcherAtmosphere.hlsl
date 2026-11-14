#ifndef WATCHER_ATMOSPHERE_INCLUDED
#define WATCHER_ATMOSPHERE_INCLUDED
TEXTURE2D(_WatcherSkyPlate);SAMPLER(sampler_WatcherSkyPlate);
float3 WatcherSkyPlate(float3 direction)
{
 // Map the entire hemisphere into a disk inside the storm image. The old
 // perspective projection ran outside 0..1 and erased clouds around the horizon.
 direction=normalize(direction);
 float radius=acos(clamp(direction.y,-.3,1))*(.42/1.570796327);
 float2 radial=direction.xz/max(length(direction.xz),.0001);
 float2 uv=.5+radial*radius;
 uv+=float2(sin(uv.y*7+_Time.y*.012),cos(uv.x*8+_Time.y*.009))*.004;
 float3 plate=SAMPLE_TEXTURE2D(_WatcherSkyPlate,sampler_WatcherSkyPlate,uv).rgb;
 float2 haloOffset=(uv-float2(.52,.58))*float2(3.0,4.6);
 float scatter=exp(-dot(haloOffset,haloOffset))*saturate(dot(plate,float3(.2126,.7152,.0722))*5+.11);
 float3 cloudLight=plate*.72+float3(.080,.010,.004)*scatter;
 return lerp(float3(.012,.021,.034),cloudLight,smoothstep(-.22,.10,direction.y));
}
float CloudHash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
float CloudNoise(float3 p)
{
 float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);
 return lerp(lerp(lerp(CloudHash(a),CloudHash(a+float3(1,0,0)),f.x),lerp(CloudHash(a+float3(0,1,0)),CloudHash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(CloudHash(a+float3(0,0,1)),CloudHash(a+float3(1,0,1)),f.x),lerp(CloudHash(a+float3(0,1,1)),CloudHash(a+1),f.x),f.y),f.z);
}
float CloudField(float3 world)
{
 float3 p=world*float3(.026,.045,.026)+float3(_Time.y*.006,0,_Time.y*.002);
 float n=CloudNoise(p)*.48+CloudNoise(p*2.07+13.1)*.27+CloudNoise(p*4.13+7.3)*.16+CloudNoise(p*8.3)*.09;
 float height=smoothstep(76,84,world.y)*(1-smoothstep(100,112,world.y));
 return smoothstep(.38,.68,n)*height;
}
// Premultiplied light and opacity of the shared foreground storm layer.
float4 WatcherClouds(float3 origin,float3 ray,float maxDistance,float flash)
{
 if(ray.y<.08)return 0;
 float start=max(0,(76-origin.y)/ray.y),finish=min(maxDistance,(112-origin.y)/ray.y);
 if(finish<=start)return 0;
 float stepSize=(finish-start)/16,trans=1;float3 light=0;
 [unroll]for(int j=0;j<16;j++)
 {
  float3 p=origin+ray*(start+(j+.5)*stepSize);
  float density=CloudField(p),alpha=1-exp(-density*stepSize*.055);
  float above=CloudField(p+float3(-7,11,-3));
  float silver=pow(saturate(1-above),3);
  float red=exp(-dot((p.xz-float2(0,12))*.014,(p.xz-float2(0,12))*.014));
  float3 shade=float3(.002,.0035,.006)+float3(.009,.014,.021)*silver+float3(.006,.0004,.0002)*red;
  shade+=float3(.28,.36,.48)*flash;
  light+=trans*alpha*shade;trans*=1-alpha;
 }
 return float4(light,1-trans);
}
#endif


