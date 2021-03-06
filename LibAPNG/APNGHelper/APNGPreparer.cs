﻿using System;
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
		List<Texture2D> rawTextureListNonPre = new List<Texture2D>();
		List<Texture2D> rawTextureList = new List<Texture2D>();
		List<Texture2D> renderedTextureList = new List<Texture2D>();

		int numFrames, framesPrepared;

		public APNGPreparer(APNG apng)
		{
			this.apng = apng;

            if (apng == null) frameList = new List<Frame>();
            else frameList = new List<Frame>(apng.Frames);
			numFrames = frameList.Count;

			// load raw component frames IN THE LOAD THREAD YOU IDIOT
			for (int i = 0; i < numFrames; i++)
			{
				//rawTextureListNonPre.Add(Texture2D.FromStream(Core.spriteBatch.GraphicsDevice, new MemoryStream(frameList[i].GetStream().ToArray())));
				rawTextureListNonPre.Add(ImageHelper.LoadFromApngFrame(frameList[i]));
			}
		}

		public override void Prepare(ImageContainer tex, AnimatedTexture anim)
		{
			SpriteBatch sb = Core.spriteBatch;
			Texture2D baseFrame = anim.frames[0].texture;

			//TextureHolder.prepareOverrun = true;

			if (rawTextureList.Count < numFrames) // init phase
			{
				// preconvert component frames
				for (int i = rawTextureList.Count; i < numFrames; i++)
				{
					rawTextureList.Add(ImageHelper.ConvertToPreMultipliedAlphaGPU(rawTextureListNonPre[i]));
					
					//return;
				}
			}

			//DateTime prepStart = DateTime.Now;
			//Console.WriteLine(tex.sourcePath.FileName + ": starting cycle");

			if (framesPrepared < frameList.Count)
			{
				const double MAX_TIME = 0.001;
				//double elapsedTime = (DateTime.Now - prepStart).TotalSeconds;
				//Console.WriteLine(tex.sourcePath.FileName + ": starting frame "+ framesPrepared +", " + elapsedTime + "s taken");
				//if (elapsedTime > MAX_TIME) break;

				var currentTexture = new RenderTarget2D(
					sb.GraphicsDevice,
					baseFrame.Width,
					baseFrame.Height);

				sb.GraphicsDevice.SetRenderTarget(currentTexture);
				sb.GraphicsDevice.Clear(Color.Transparent);

				// if this is the first frame, just draw.
				if (framesPrepared == 0)
				{
					goto LABEL_DRAW_NEW_FRAME;
				}

				// Restore previous texture
				sb.Begin();
				sb.Draw(renderedTextureList[framesPrepared - 1], Vector2.Zero, Color.White);
				sb.End();

				Frame crtFrame = frameList[framesPrepared - 1];
				Texture2D crtFrameRaw = rawTextureList[framesPrepared - 1];

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
						if (framesPrepared - 1 == 0)
						{
							goto LABEL_APNG_DISPOSE_OP_BACKGROUND;
						}

						Frame prevFrame = frameList[framesPrepared - 2];
						Texture2D prevFrameRaw = rawTextureList[framesPrepared - 2];

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
				if (framesPrepared == 0)
				{
					crtFrame = frameList[0];
					crtFrameRaw = rawTextureList[0];
				}
				else
				{
					int ind = framesPrepared < frameList.Count
								   ? framesPrepared
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
				framesPrepared++;
			}

			sb.GraphicsDevice.SetRenderTarget(null);
            // prod the core to redraw
            Stellariview.Core.redraw = true;

			if (framesPrepared == numFrames)
			{
				// retexture animation frames
				for (int i = 0; i < renderedTextureList.Count; i++) anim.frames[i] = new AnimFrame(renderedTextureList[i], anim.frames[i].duration);
				tex.state = ImageContainer.TextureState.Loaded;
			}
		}
    }
}