# Encodes the frames + soundtrack rendered by Unity (KryvonVideo.Render) into an MP4 on the desktop.
import subprocess, sys, os, imageio_ffmpeg
src = os.path.join(os.path.dirname(__file__), '..', 'unity', 'VideoFrames')
out = sys.argv[1] if len(sys.argv) > 1 else os.path.expanduser(r'~\Desktop\KRYVON_gameplay.mp4')
font = r'C\:/Windows/Fonts/arialbd.ttf'
title = (f"drawtext=fontfile='{font}':text='KRAYVON':fontsize=110:fontcolor=white:x=(w-tw)/2:y=h*0.36:"
         "alpha='if(lt(t,0.4),t/0.4,if(lt(t,3.2),1,max(0,(4-t)/0.8)))':shadowcolor=black@0.6:shadowx=3:shadowy=3,"
         f"drawtext=fontfile='{font}':text='BILAD AL-SHAM':fontsize=34:fontcolor=0xF2D9A0:x=(w-tw)/2:y=h*0.36+125:"
         "alpha='if(lt(t,0.6),t/0.6,if(lt(t,3.2),1,max(0,(4-t)/0.8)))',fade=t=in:st=0:d=0.6,fade=t=out:st=21.2:d=0.8")
cmd = [imageio_ffmpeg.get_ffmpeg_exe(), '-y', '-framerate', '30', '-i', os.path.join(src, 'f_%05d.jpg'), '-i', os.path.join(src, 'mix.wav'),
       '-vf', title, '-c:v', 'libx264', '-preset', 'slow', '-crf', '18', '-pix_fmt', 'yuv420p', '-c:a', 'aac', '-b:a', '192k', '-shortest', out]
subprocess.run(cmd, check=True, capture_output=True)
print('saved', out, os.path.getsize(out) // 1024, 'KB')
