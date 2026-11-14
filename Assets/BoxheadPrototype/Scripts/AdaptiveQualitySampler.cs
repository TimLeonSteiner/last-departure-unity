using System;
namespace YardSurvivalPrototype
{
    // Measures delivered gameplay frames, not estimated hardware specifications.
    public sealed class AdaptiveQualitySampler
    {
        readonly float[] samples=new float[2048];
        int count,frames,badWindows;
        float elapsed,warmup=4;
        public float Fps {get;private set;}
        public float P95Milliseconds {get;private set;}
        public int Windows {get;private set;}
        public void Reset(float settle=4){count=0;frames=0;elapsed=0;badWindows=0;warmup=settle;Fps=0;P95Milliseconds=0;Windows=0;}
        public void Suspend(){count=0;frames=0;elapsed=0;badWindows=0;warmup=2;}
        public bool Add(float delta,bool active)
        {
            if(!active){Suspend();return false;}
            if(delta<=0 || float.IsNaN(delta)||float.IsInfinity(delta)||delta>1.5f){Suspend();return false;}
            if(warmup>0){warmup-=delta;return false;}
            elapsed+=delta;frames++;
            if(count<samples.Length)samples[count++]=delta;
            if(elapsed<6 || count<12)return false;
            Fps=frames/elapsed;
            Array.Sort(samples,0,count);
            P95Milliseconds=samples[Math.Min(count-1,(int)Math.Ceiling(count*.95)-1)]*1000;
            Windows++;
            bool poor=Fps<28 || P95Milliseconds>65;
            badWindows=poor?badWindows+1:0;
            bool lower=Fps<12 || badWindows>=2;
            count=0;frames=0;elapsed=0;
            if(lower){badWindows=0;warmup=6;}
            return lower;
        }
        public static int Lower(int preset){return preset==2?1:preset==1?0:3;}
    }
}
