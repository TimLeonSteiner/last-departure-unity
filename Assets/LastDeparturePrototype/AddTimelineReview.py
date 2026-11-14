from pathlib import Path
p=Path('LastDeparturePrototype/Scripts/YardSurvival.ArsenalChecks.cs');s=p.read_text(encoding='utf-8-sig');needle='                ResetRun();EquipWeapon(18,1);'
code='''                var priorMask=arenaCamera.cullingMask;var priorClear=arenaCamera.clearFlags;var priorBackground=arenaCamera.backgroundColor;
                arenaCamera.cullingMask=1<<30;arenaCamera.clearFlags=CameraClearFlags.SolidColor;arenaCamera.backgroundColor=new Color(.11f,.13f,.15f);
                foreach(int id in new[]{12,13,14,15,16,17,18,21})
                {
                    ResetRun();EquipWeapon(id,WeaponCatalog.All[id].capacity);var rig=gun.GetComponentInChildren<AnimatedWeaponRig>();rig.Pose(0,false,1);viewWeapon.Tick(0,false,0,0);
                    for(int frame=0;frame<=rig.fire.length*rig.fire.frameRate;frame+=10)
                    {
                        rig.fire.SampleAnimation(rig.model,frame/rig.fire.frameRate);
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(arenaCamera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
                        RenderTexture.active=target;image.ReadPixels(new Rect(0,0,800,500),0,0);image.Apply();File.WriteAllBytes(folder+"/Timeline-"+id+"-"+frame+".png",image.EncodeToPNG());
                    }
                }
                arenaCamera.cullingMask=priorMask;arenaCamera.clearFlags=priorClear;arenaCamera.backgroundColor=priorBackground;
'''
assert needle in s;s=s.replace(needle,code+needle,1);p.write_text(s,encoding='utf-8')
