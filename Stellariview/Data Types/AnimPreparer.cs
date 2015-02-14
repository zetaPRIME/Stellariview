using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Stellariview
{
	public abstract class AnimPreparer
	{
		public abstract void Prepare(TextureHolder tex, AnimatedTexture anim);
	}

	public class BasicAnimPreparer : AnimPreparer
	{
		int framesDone = 0;

		public override void Prepare(TextureHolder tex, AnimatedTexture anim)
		{
			// convert all frames to premultiplied
			for (int i = framesDone; i < anim.frames.Count; i++)
			{
				anim.frames[i] = new AnimFrame(ImageHelper.ConvertToPreMultipliedAlphaGPU(anim.frames[i].texture), anim.frames[i].duration);
				framesDone++;
				return;
			}
			tex.state = TextureHolder.TextureState.Loaded;
		}
	}
}
