import sys
from paint import *
todo = sys.argv[1:] or list(CONF)
for nm in todo:
    b = build(nm, write=True, report=False)
    im = paint_mouth(nm, b) if CONF[nm]['plan'] in ('round', 'flat') else None
    if nm == 'Clownfish':
        im = Image.fromarray(np.clip(repaint_clown_dorsal(np.asarray(im), edge=-1, rim=-1), 0, 255).astype(np.uint8))
    atlas(nm, im)
    print(nm, 'done')
