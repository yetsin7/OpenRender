namespace OpenRender.Services;

/// <summary>
/// Suaviza pequeñas perforaciones del preview para que la masa del modelo
/// se lea como superficie continua en vez de ruido disperso.
/// </summary>
public static class ViewportPreviewPostProcessor
{
    public static void SealForeground(int[] pixels, float[] depthBuffer, int width, int height)
    {
        int[] scratchPixels = new int[pixels.Length];
        float[] scratchDepth = new float[depthBuffer.Length];

        for (int pass = 0; pass < 2; pass++)
        {
            pixels.CopyTo(scratchPixels, 0);
            depthBuffer.CopyTo(scratchDepth, 0);

            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int index = y * width + x;
                    if (depthBuffer[index] != float.MaxValue)
                        continue;

                    int hits = 0;
                    int r = 0;
                    int g = 0;
                    int b = 0;
                    float nearest = float.MaxValue;

                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            if (ox == 0 && oy == 0)
                                continue;

                            int neighborIndex = index + oy * width + ox;
                            if (depthBuffer[neighborIndex] == float.MaxValue)
                                continue;

                            hits++;
                            int color = pixels[neighborIndex];
                            b += color & 0xFF;
                            g += (color >> 8) & 0xFF;
                            r += (color >> 16) & 0xFF;
                            nearest = MathF.Min(nearest, depthBuffer[neighborIndex]);
                        }
                    }

                    if (hits < 5)
                        continue;

                    scratchPixels[index] = unchecked((int)(0xFF000000u | ((uint)(r / hits) << 16) | ((uint)(g / hits) << 8) | (uint)(b / hits)));
                    scratchDepth[index] = nearest;
                }
            }

            scratchPixels.CopyTo(pixels, 0);
            scratchDepth.CopyTo(depthBuffer, 0);
        }
    }

    public static void SoftenHighlights(int[] pixels, int width, int height)
    {
        int[] scratch = new int[pixels.Length];
        pixels.CopyTo(scratch, 0);

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int index = y * width + x;
                int color = pixels[index];
                int r = (color >> 16) & 0xFF;
                int g = (color >> 8) & 0xFF;
                int b = color & 0xFF;
                int luma = (r * 54 + g * 183 + b * 19) >> 8;
                if (luma < 220)
                    continue;

                int blurR = r;
                int blurG = g;
                int blurB = b;
                int samples = 1;

                for (int oy = -1; oy <= 1; oy++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        if (ox == 0 && oy == 0)
                            continue;

                        int neighbor = pixels[index + oy * width + ox];
                        blurR += (neighbor >> 16) & 0xFF;
                        blurG += (neighbor >> 8) & 0xFF;
                        blurB += neighbor & 0xFF;
                        samples++;
                    }
                }

                int nr = Math.Min(255, (int)(r * 0.55f + (blurR / (float)samples) * 0.45f));
                int ng = Math.Min(255, (int)(g * 0.55f + (blurG / (float)samples) * 0.45f));
                int nb = Math.Min(255, (int)(b * 0.55f + (blurB / (float)samples) * 0.45f));
                scratch[index] = unchecked((int)(0xFF000000u | ((uint)nr << 16) | ((uint)ng << 8) | (uint)nb));
            }
        }

        scratch.CopyTo(pixels, 0);
    }
}
