using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Threading;

using Fluent.IO;
using Ionic.Zip;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

using LibAPNG;

using Path = Fluent.IO.Path;
using Color = Microsoft.Xna.Framework.Color;

namespace Stellariview
{
	public static class ImageHelper
	{
		public static Texture2D ConvertToPreMultipliedAlphaGPU(Texture2D texture)
		{
			// code borrowed from http://jakepoz.com/jake_poznanski__speeding_up_xna.html

			GraphicsDevice GraphicsDevice = Core.spriteBatch.GraphicsDevice;

			//Setup a render target to hold our final texture which will have premulitplied alpha values
			RenderTarget2D result = new RenderTarget2D(GraphicsDevice, texture.Width, texture.Height);

			GraphicsDevice.SetRenderTarget(result);
			GraphicsDevice.Clear(Color.Black);

			//Multiply each color by the source alpha, and write in just the color values into the final texture
			BlendState blendColor = new BlendState();
			blendColor.ColorWriteChannels = ColorWriteChannels.Red | ColorWriteChannels.Green | ColorWriteChannels.Blue;

			blendColor.AlphaDestinationBlend = Blend.Zero;
			blendColor.ColorDestinationBlend = Blend.Zero;

			blendColor.AlphaSourceBlend = Blend.SourceAlpha;
			blendColor.ColorSourceBlend = Blend.SourceAlpha;

			SpriteBatch spriteBatch = new SpriteBatch(GraphicsDevice);
			spriteBatch.Begin(SpriteSortMode.Immediate, blendColor);
			spriteBatch.Draw(texture, texture.Bounds, Color.White);
			spriteBatch.End();

			//Now copy over the alpha values from the PNG source texture to the final one, without multiplying them
			BlendState blendAlpha = new BlendState();
			blendAlpha.ColorWriteChannels = ColorWriteChannels.Alpha;

			blendAlpha.AlphaDestinationBlend = Blend.Zero;
			blendAlpha.ColorDestinationBlend = Blend.Zero;

			blendAlpha.AlphaSourceBlend = Blend.One;
			blendAlpha.ColorSourceBlend = Blend.One;

			spriteBatch.Begin(SpriteSortMode.Immediate, blendAlpha);
			spriteBatch.Draw(texture, texture.Bounds, Color.White);
			spriteBatch.End();

			//Release the GPU back to drawing to the screen
			GraphicsDevice.SetRenderTarget(null);

			return result as Texture2D;
		}

		public static Texture2D LoadFromStream(FileStream fs)
		{
			Texture2D res = null;
			using (MemoryStream ms = new MemoryStream())
			{
				fs.CopyTo(ms);
				res = LoadFromStream(ms);
			}
			return res;
		}
		public static Texture2D LoadFromStream(MemoryStream ms)
		{
			Texture2D res = null;

			try
			{
				res = Texture2D.FromStream(Core.spriteBatch.GraphicsDevice, ms);
			}
			catch (Exception e)
			{
				if (e.Message.Contains("indexed"))
				{
					Image img = Image.FromStream(ms);
					Bitmap bmp = new Bitmap(img);
					using (MemoryStream ms2 = new MemoryStream())
					{
						bmp.Save(ms2, System.Drawing.Imaging.ImageFormat.Png);
						res = Texture2D.FromStream(Core.spriteBatch.GraphicsDevice, ms2);
					}
				}
				else throw e;
			}

			return res;
		}
		public static Texture2D LoadFromApngFrame(Frame f)
		{
			return LoadFromStream(new MemoryStream(f.GetStream().ToArray()));
		}
	}
}
