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

using Path = Fluent.IO.Path;
using Color = Microsoft.Xna.Framework.Color;

namespace Stellariview
{
	public class TextureHolder
	{
		const bool DEBUG = true;
		DateTime loadStartTime;

		double loadStartGameTime = 0;

		public enum TextureState { Unloaded, Loading, Preparing, Loaded, Error }

		public TextureState state = TextureState.Unloaded;

		public Texture2D texture;
		public AnimatedTexture animation;
		public Path sourcePath;

		public bool mainTexPremultiplied = false;

		public TextureHolder(Path path, bool loadImmediate)
		{
			sourcePath = path;
			if (loadImmediate) Load();
		}

		#region Loading stuffs
		public void LoadImage(Path path)
		{
			sourcePath = path;
			Load();
		}

		public void Load()
		{
			if (state == TextureState.Loaded || state == TextureState.Error) { Prioritize(); return; }
			if (state != TextureState.Loading && Core.frameTime != null) loadStartGameTime = Core.frameTime.TotalGameTime.TotalSeconds;
			state = TextureState.Loading;

			if (loadQueue.Contains(this)) loadQueue.Remove(this);
			loadQueue.Insert(0, this);
		}

		void Prioritize()
		{
			if (state == TextureState.Error) return;
			// freshen so as not to unload at weird times
			if (allLoaded.Contains(this)) allLoaded.Remove(this);
			allLoaded.Add(this);
		}

		public void Unload() {
			texture = null;
			animation = null;
			state = TextureState.Unloaded;

			if (loadQueue.Contains(this)) loadQueue.Remove(this);
			allLoaded.Remove(this);
		}

		void LoadFromThread()
		{
			if (DEBUG) loadStartTime = DateTime.Now;
			Texture2D res = null;
			AnimatedTexture resAnim = null;
			if (!sourcePath.Exists)
			{
				state = TextureState.Error;

				allLoaded.Add(this);
				loadQueue.Remove(this);
				return;
			}
			if (new[]{ ".gif", ".png" }.Contains(sourcePath.Extension))
			{
				resAnim = new AnimatedTexture(sourcePath);

				res = resAnim.frames[0].texture;

				if (resAnim.frames.Count == 1) resAnim = null; // discard
			}
			else
			{
				sourcePath.Open((FileStream fs) =>
				{

					try
					{
						res = Texture2D.FromStream(Core.spriteBatch.GraphicsDevice, fs);
						//res = ConvertToPreMultipliedAlphaGPU(res);
					}
					catch (Exception e)
					{
						if (e.Message.Contains("indexed"))
						{
							Image img = Image.FromStream(fs);
							Bitmap bmp = new Bitmap(img);
							using (MemoryStream ms = new MemoryStream())
							{
								bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
								res = Texture2D.FromStream(Core.spriteBatch.GraphicsDevice, ms);
								//res = ConvertToPreMultipliedAlphaGPU(res);
							}
						}
					}
				});
			}
			if (res != null && state == TextureState.Loading)
			{
				texture = res;
				animation = resAnim;
				if (DEBUG) Console.WriteLine("Loading of " + sourcePath.FileName + " took " + (DateTime.Now - loadStartTime).TotalSeconds + "s; dimensions " + res.Width + "x" + res.Height);
				state = TextureState.Preparing;

				mainTexPremultiplied = false;

				allLoaded.Add(this);
				loadQueue.Remove(this);
			}
			else Unload();
		}
		#endregion

		public Vector2 GetSize(Vector2 sizeConstraints)
		{
			if (state == TextureState.Loaded)
			{
				Vector2 imgSize = new Vector2(texture.Width, texture.Height);

				float scale = Math.Min(sizeConstraints.X / imgSize.X, sizeConstraints.Y / imgSize.Y);
				scale = Math.Min(scale, 1f);

				return imgSize * scale;
			}

			return Vector2.One * 80;
		}

		public void Draw(SpriteBatch sb, Vector2 position, Vector2 sizeConstraints, Color? color = null)
		{
			Color drawColor = Color.White;
			if (color != null) drawColor = color.Value;

			if (state == TextureState.Loaded)
			{
				Vector2 imgSize = new Vector2(texture.Width, texture.Height);

				float scale = Math.Min(sizeConstraints.X / imgSize.X, sizeConstraints.Y / imgSize.Y);
				scale = Math.Min(scale, 1f);

				if (animation != null)
				{
					animation.Draw(sb, position, null, drawColor, 0f, imgSize / 2f, scale, SpriteEffects.None, 0f);
				}
				else
				{
					sb.Draw(texture, position, null, drawColor, 0f, imgSize / 2f, scale, SpriteEffects.None, 0f);
				}
			}

			else if (state == TextureState.Loading || state == TextureState.Preparing)
			{
				double time = Core.frameTime.TotalGameTime.TotalSeconds - loadStartGameTime;
				float rotation = (float)(time * Math.PI * 1.0);
				
				const int numDots = 8;
				const float rotStep = (float)(Math.PI * 2 / numDots);

				Vector4 drawColorVec = drawColor.ToVector4();

				for (int i = 0; i < numDots; i++)
				{
					sb.Draw(Core.txPixel, position + new Vector2((float)Math.Cos(rotation - rotStep * i), (float)Math.Sin(rotation - rotStep * i)) * 20f, null, new Color(drawColorVec * (1f - i * (1f/8f))), (float)(Math.PI / 4), Vector2.One * 0.5f, new Vector2(5f, 5f), SpriteEffects.None, 0f);
				}
			}

			else if (state == TextureState.Error)
			{
				Vector4 drawColorVec = drawColor.ToVector4();
				Vector4 colorVec = new Vector4(1f, 0f, 0f, 0f) * drawColorVec;
				Color dcolor = new Color(colorVec);

				sb.Draw(Core.txPixel, position, null, dcolor, (float)Math.PI * 0.25f, Vector2.One * 0.5f, new Vector2(4f, 64f), SpriteEffects.None, 0f);
				sb.Draw(Core.txPixel, position, null, dcolor, (float)Math.PI * -0.25f, Vector2.One * 0.5f, new Vector2(4f, 64f), SpriteEffects.None, 0f);
			}
		}

		public void Prepare()
		{
			if (!mainTexPremultiplied) { texture = ImageHelper.ConvertToPreMultipliedAlphaGPU(texture); mainTexPremultiplied = true; }
			if (animation != null) animation.Prepare(this);
			else state = TextureState.Loaded;
		}

		Texture2D ConvertToPreMultipliedAlpha(Texture2D texture)
		{
			Color[] data = new Color[texture.Width * texture.Height];
			texture.GetData<Color>(data, 0, data.Length);
			for (int i = 0; i < data.Length; i++)
			{
				byte A = data[i].A;
				data[i] = data[i] * (data[i].A / 255f);
				data[i].A = A;
			}
			texture.SetData<Color>(data, 0, data.Length);
			return texture;
		}

		#region Static things
		static Thread loadThread = null;

		public static bool pauseLoad = false;
		public static List<TextureHolder> loadQueue = new List<TextureHolder>();
		public static List<TextureHolder> allLoaded = new List<TextureHolder>();
		const int MAX_LOADED = 16;

		public static void ProcessConvertQueue()
		{
			List<TextureHolder> queue;
			lock (allLoaded) queue = new List<TextureHolder>(allLoaded);
			foreach (TextureHolder tex in queue)
			{
				if (tex.state == TextureState.Preparing)
				{
					tex.Prepare();
				}
			}
		}

		public static void StartLoadThread()
		{
			loadThread = new Thread(LoadThread);
			loadThread.IsBackground = true;
			loadThread.Priority = ThreadPriority.BelowNormal;
			loadThread.Start();
		}

		public static void LoadThread()
		{
			while (true) {
				if (pauseLoad || loadQueue.Count == 0) { Thread.Sleep(100); continue; }

				loadQueue[0].LoadFromThread();
				CleanUp();
			}
		}

		public static void CleanUp()
		{
			while (allLoaded.Count > MAX_LOADED) allLoaded[0].Unload();
		}
		#endregion
	}
}
