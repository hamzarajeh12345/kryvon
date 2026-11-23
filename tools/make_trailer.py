# Encodes the trailer recorded by the game (KRAYVON.exe -trailer) into an MP4 on the desktop:
# frames + the game's own sound, a title at the start, a caption per scene, the logo at the end.
import os, shutil, subprocess, sys, imageio_ffmpeg

src = os.path.expandvars(r'%USERPROFILE%\AppData\LocalLow\KRAYVON\KRAYVON\trailer')
out = sys.argv[1] if len(sys.argv) > 1 else os.path.expanduser(r'~\Desktop\KRAYVON_trailer.mp4')
here = os.path.dirname(os.path.abspath(__file__))
font_src = os.path.join(here, '..', 'unity', 'Assets', '_Kryvon', 'ThirdParty', 'Fonts', 'BebasNeue-Regular.ttf')
font = os.path.join(src, 'title.ttf'); shutil.copyfile(font_src, font)
ff = font.replace('\\', '/').replace(':', '\\:')

FPS = 30
shots = []
for line in open(os.path.join(src, 'shots.txt')):
    f, name = line.split(); shots.append((int(f), name))
total = len([n for n in os.listdir(src) if n.startswith('f_')])
dur = total / FPS

CAPTIONS = {'lobby': 'CHOOSE YOUR FIGHTER', 'waiting': 'THE WAITING AREA', 'plane': 'THE DROP',
            'freefall': 'BILAD AL-SHAM', 'drive': 'TAKE THE WHEEL', 'fight': 'FIGHT TO SURVIVE', 'petra': 'PETRA'}

def text(t, size, y, start, end, color='white'):
    a = f"if(lt(t,{start}+0.35),(t-{start})/0.35,if(lt(t,{end}-0.35),1,({end}-t)/0.35))"
    return (f"drawtext=fontfile='{ff}':text='{t}':fontsize={size}:fontcolor={color}:x=(w-tw)/2:y={y}:"
            f"alpha='{a}':enable='between(t,{start},{end})':shadowcolor=black@0.7:shadowx=3:shadowy=3")

vf = []
for i, (f, name) in enumerate(shots):
    s = f / FPS; e = (shots[i + 1][0] / FPS) if i + 1 < len(shots) else dur
    if name in CAPTIONS and e - s > 1.6:
        cs = s + (2.6 if name == 'lobby' else 0.3); vf.append(text(CAPTIONS[name], 64, 'h*0.80', round(cs, 2), round(min(e, cs + 2.4), 2), '0xF5C451'))
# title over the opening, logo over the ending
vf.insert(0, text('KRAYVON', 170, 'h*0.30', 0.2, 2.6))
vf.append(text('KRAYVON', 190, 'h*0.34', round(dur - 3.6, 2), round(dur - 0.2, 2)))
vf.append(text('BATTLE ROYALE  ·  BILAD AL-SHAM', 52, 'h*0.34+200', round(dur - 3.3, 2), round(dur - 0.2, 2), '0xB98CFF'))
vf.append(f"fade=t=in:st=0:d=0.6,fade=t=out:st={dur - 0.8:.2f}:d=0.8")

cmd = [imageio_ffmpeg.get_ffmpeg_exe(), '-y', '-framerate', str(FPS), '-i', os.path.join(src, 'f_%05d.jpg')]
wav = os.path.join(src, 'audio.f32')
if os.path.exists(wav):
    rate, ch = open(os.path.join(src, 'audio.txt')).read().split()
    cmd += ['-f', 'f32le', '-ar', rate, '-ac', ch, '-i', wav]
cmd += ['-vf', ','.join(vf), '-c:v', 'libx264', '-preset', 'slow', '-crf', '17', '-pix_fmt', 'yuv420p']
if os.path.exists(wav): cmd += ['-c:a', 'aac', '-b:a', '192k', '-af', f'afade=t=out:st={dur - 1:.2f}:d=1', '-shortest']
cmd += [out]
r = subprocess.run(cmd, capture_output=True, text=True)
if r.returncode: print(r.stderr[-2000:]); sys.exit(1)
print('saved', out, os.path.getsize(out) // 1024, 'KB', f'{dur:.1f}s', total, 'frames')
