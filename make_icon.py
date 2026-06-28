from PIL import Image, ImageDraw

size = 256
img = Image.new('RGBA', (size, size), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# Mouse head and ears
head = [(size * 0.18, size * 0.28), (size * 0.82, size * 0.77)]
d.ellipse(head, fill=(60, 60, 60, 255), outline=(0, 0, 0, 255), width=8)
ear1 = [(size * 0.08, size * 0.08), (size * 0.38, size * 0.42)]
ear2 = [(size * 0.62, size * 0.08), (size * 0.92, size * 0.42)]
d.ellipse(ear1, fill=(60, 60, 60, 255), outline=(0, 0, 0, 255), width=8)
d.ellipse(ear2, fill=(60, 60, 60, 255), outline=(0, 0, 0, 255), width=8)

# Eyes and nose
eye_r = size * 0.05
d.ellipse([(size * 0.38 - eye_r, size * 0.47 - eye_r), (size * 0.38 + eye_r, size * 0.47 + eye_r)], fill=(255, 255, 255, 255))
d.ellipse([(size * 0.62 - eye_r, size * 0.47 - eye_r), (size * 0.62 + eye_r, size * 0.47 + eye_r)], fill=(255, 255, 255, 255))
d.polygon([(size * 0.50, size * 0.58), (size * 0.45, size * 0.64), (size * 0.55, size * 0.64)], fill=(255, 255, 255, 255))

# Highlight
highlight = [(size * 0.42, size * 0.30), (size * 0.60, size * 0.38)]
d.ellipse(highlight, fill=(200, 200, 200, 255))

img.save('app.ico', sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
print('created app.ico')
