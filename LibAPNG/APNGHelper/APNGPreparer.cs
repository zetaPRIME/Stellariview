using System;
using System.Collections.Generic;
using System.IO;
using LibAPNG;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Stellariview;

namespace LibAPNG.XNAHelper
{
	// (badly) hacked up to enable loading into AnimatedTexture
    public class APNGPreparer : AnimPreparer
    {
		public APNG apng;

		List<Frame> frameList;
		List<Texture2D> rawTextureList = new List<Texture2D>();
		List<Texture2D> renderedTextureList = new List<Texture2D>();

		int numFrames, framesPrepared;

		public APNGPreparer(APNG apng)
		{
			this.apng = apng;

			frameList = new List<Frame>(apng.Frames);
			numFrames = frameList.Count;
		}

		public override void Prepare(TextureHolder tex, AnimatedTexture anim)
		{
			//
			tex.state = TextureHolder.TextureState.Loaded;
		}

        public void RenderApngFrames(AnimatedTexture anim, SpriteBatch sb)
        {
			if (apng == null) return; // gtfo

			Texture2D baseFrame = anim.frames[0].texture;

			List<Frame> frameList = new List<Frame>(apng.Frames);
			List<Texture2D> rawTextureList = new List<Texture2D>();
			List<Texture2D> renderedTextureList = new List<Texture2D>();

			// load/preconvert all component frames
			foreach (Frame f in frameList)
			{
				rawTextureList.Add(ImageHelper.ConvertToPreMultipliedAlphaGPU(Texture2D.FromStream(sb.GraphicsDevice, new MemoryStream(f.GetStream().ToArray()))));
			}

			for (int crtIndex = 0; crtIndex < frameList.Count; crtIndex++)
            {
                var currentTexture = new RenderTarget2D(
                    sb.GraphicsDevice,
                    baseFrame.Width,
                    baseFrame.Height);

                sb.GraphicsDevice.SetRenderTarget(currentTexture);
                sb.GraphicsDevice.Clear(Color.Transparent);

                // if this is the first frame, just draw.
                if (crtIndex == 0)
                {
                    goto LABEL_DRAW_NEW_FRAME;
                }

                // Restore previous texture
                sb.Begin();
                sb.Draw(renderedTextureList[crtIndex - 1], Vector2.Zero, Color.White);
                sb.End();

                Frame crtFrame = frameList[crtIndex - 1];
				Texture2D crtFrameRaw = rawTextureList[crtIndex - 1];

				switch (crtFrame.fcTLChunk.DisposeOp)
                {
                        // Do nothing.
                    case DisposeOps.APNGDisposeOpNone:
                        break;

                        // Set current Rectangle to transparent.
                    case DisposeOps.APNGDisposeOpBackground:
                        LABEL_APNG_DISPOSE_OP_BACKGROUND:
                        var t2 = new Texture2D(sb.GraphicsDevice, 1, 1);
                        sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
                        sb.Draw(
                                t2,
								new Rectangle((int)crtFrame.fcTLChunk.XOffset, (int)crtFrame.fcTLChunk.YOffset, crtFrameRaw.Width, crtFrameRaw.Height),
                                Color.White);
                        sb.End();
                        break;

                        // Rollback to previous frame.
                    case DisposeOps.APNGDisposeOpPrevious:
                        // If the first `fcTL` chunk uses a `dispose_op` of APNG_DISPOSE_OP_PREVIOUS
                        // it should be treated as APNG_DISPOSE_OP_BACKGROUND.
                        if (crtIndex - 1 == 0)
                        {
                            goto LABEL_APNG_DISPOSE_OP_BACKGROUND;
                        }

                        Frame prevFrame = frameList[crtIndex - 2];
						Texture2D prevFrameRaw = rawTextureList[crtIndex - 2];

                        sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
                        sb.Draw(
                                prevFrameRaw,
								new Rectangle((int)crtFrame.fcTLChunk.XOffset, (int)crtFrame.fcTLChunk.YOffset, crtFrameRaw.Width, crtFrameRaw.Height),
								new Rectangle((int)crtFrame.fcTLChunk.XOffset, (int)crtFrame.fcTLChunk.YOffset, crtFrameRaw.Width, crtFrameRaw.Height),
                                Color.White);
                        sb.End();
                        break;
                }

                LABEL_DRAW_NEW_FRAME:
                // Now let's look at the new frame.
                if (crtIndex == 0)
                {
                    crtFrame = frameList[0];
					crtFrameRaw = rawTextureList[0];
                }
                else
                {
					int ind = crtIndex < frameList.Count
                                   ? crtIndex
                                   : 0;
					crtFrame = frameList[ind];
					crtFrameRaw = rawTextureList[ind];
                }
				
				switch (crtFrame.fcTLChunk.BlendOp)
                {
                        // Do not apply alpha
                    case BlendOps.APNGBlendOpSource:
                        sb.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
                        sb.Draw(
                                crtFrameRaw,
								new Rectangle((int)crtFrame.fcTLChunk.XOffset, (int)crtFrame.fcTLChunk.YOffset, crtFrameRaw.Width, crtFrameRaw.Height),
                                Color.White);
                        sb.End();
                        break;

                        // Apply alpha
                    case BlendOps.APNGBlendOpOver:
                        sb.Begin();
                        sb.Draw(
                                crtFrameRaw,
								new Rectangle((int)crtFrame.fcTLChunk.XOffset, (int)crtFrame.fcTLChunk.YOffset, crtFrameRaw.Width, crtFrameRaw.Height),
                                Color.White);
                        sb.End();
                        break;
                }

                renderedTextureList.Add(currentTexture);
            }
			
            // Okay it's all over now
            sb.GraphicsDevice.SetRenderTarget(null);

			// and retexture animation frames
			for (int i = 0; i < renderedTextureList.Count; i++) anim.frames[i] = new AnimFrame(renderedTextureList[i], anim.frames[i].duration);
        }
    }
}