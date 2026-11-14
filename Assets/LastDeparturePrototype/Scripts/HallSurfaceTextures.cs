using UnityEngine;

namespace YardSurvivalPrototype
{
    public static class HallSurfaceTextures
    {
        public static bool Bind()
        {
            var layout=FullRoofLayout.Load();
            for(int i=0;i<3;i++)
            {
                var center=layout!=null&&i<layout.rainPatches.Length?layout.rainPatches[i].center*YardSurvival.ArenaScale:new Vector2(1000,1000);
                Shader.SetGlobalVector("_HallLeak"+i,new Vector4(center.x,center.y,0,0));
            }
            var floor=Resources.Load<Texture2D>("HallTextures/ConcreteBase");
            var floorSurface=Resources.Load<Texture2D>("HallTextures/ConcreteSurface");
            var wall=Resources.Load<Texture2D>("HallTextures/PaintBase");
            var wallSurface=Resources.Load<Texture2D>("HallTextures/PaintSurface");
            bool ready=floor!=null&&floorSurface!=null&&wall!=null&&wallSurface!=null;
            Shader.SetGlobalFloat("_HallTextureReady",ready?1:0);
            if(!ready)return false;
            Shader.SetGlobalTexture("_HallFloorBase",floor);
            Shader.SetGlobalTexture("_HallFloorSurface",floorSurface);
            Shader.SetGlobalTexture("_HallWallBase",wall);
            Shader.SetGlobalTexture("_HallWallSurface",wallSurface);
            return true;
        }
    }
}
