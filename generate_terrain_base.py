import matplotlib.pyplot as plt
import numpy as np
from perlin_numpy import (
    generate_perlin_noise_2d, generate_fractal_noise_2d
)
import imageio
from PIL import Image

imgwidth = 1024*4
pfactor = 16
noiseheight = 0.05

np.random.seed(0)

noise = generate_perlin_noise_2d((imgwidth, imgwidth), (pfactor, pfactor))

noise = np.array(Image.fromarray(((noise * 0.5 + 0.5) * 65535).astype(np.uint16)).resize((imgwidth+1, imgwidth+1))).astype('float32') / 65535 - 0.5

lingrad = np.linspace(0, 1, imgwidth+1)
slope = np.tile(lingrad, (imgwidth+1, 1))

final = np.clip(np.add(slope, np.multiply(noise, noiseheight)), 0, 1)

imageio.imwrite('terrain_base.png', (65535 * final).astype(np.uint16))

plt.imshow(final, cmap='gray', interpolation='lanczos')

plt.colorbar()
plt.show()
