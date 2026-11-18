"""Build the scoped 0.7.0 paint correction in the existing private Editor."""
from pathlib import Path
import os,shutil,subprocess
r=Path('D:/Artifacts/fs20-publication/0.7.0');fix=r/'texture-fixes-20261118';src=r/'source'
project=Path('D:/Artifacts/fs20-own-liveries/2026-11-16/CreatorKit/UnityProject');ed=project/'Assets/Editor'
changes={ed/'BuildFs20PreparedPaints.cs':Path('tools/BuildFs20PreparedPaints.cs'),ed/'BakeFs20PreparedStencilCleanup.cs':Path('tools/BakeFs20PreparedStencilCleanup.cs'),ed/'Fs20PaintPixels.cs':src/'src/Fs20PaintPixels.cs'}
saved={p:p.read_bytes() if p.exists() else None for p in changes}
# Always start from the untouched input, so stencil cleanup is applied once.
for paint in (r/'paint-input/A').glob('*.png'):shutil.copy2(paint,fix/'paint-input/A'/paint.name)
try:
 for p,s in changes.items():shutil.copy2(s,p)
 env=dict(os.environ,FS20_PAINT_INPUT=str(fix/'paint-input'),FS20_PAINT_OUTPUT=str(fix/'bundle'),FS20_PAINT_WING_SIZE='2048')
 for method,log in [('BakeFs20PreparedStencilCleanup.Run','stencil'),('BuildFs20PreparedPaints.Build','bundle')]:
  result=subprocess.run(['D:/SDK/Unity/2022.3.62f2/Editor/Unity.exe','-batchmode','-nographics','-quit','-projectPath',str(project),'-executeMethod',method,'-logFile',str(fix/(log+'.log'))],env=env,creationflags=subprocess.CREATE_NO_WINDOW)
  assert result.returncode==0,method
  if log=='stencil':subprocess.run(['python','tools/refinish_fs20_a_stencils.py'],env=env,check=True)
 print('PREPARED_A_OFFLINE_BUNDLE_PASS')
finally:
 for p,data in saved.items():
  if data is None:p.unlink(missing_ok=True)
  else:p.write_bytes(data)
