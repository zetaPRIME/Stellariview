using System;
using System.Collections.Generic;
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

namespace Stellariview
{
	//

	public partial class Core : Game
	{
		string[] supportedTypes = new[] { ".png", ".jpg", ".gif" };

		public static Path startingPath;
		Path directory;

		List<TextureHolder> entriesOriginal = new List<TextureHolder>();
		List<TextureHolder> entriesCurrent;
		int currentEntryId = 0;
		int lastFrameEntryId = 0;
		TextureHolder CurrentEntry
		{
			get { return entriesCurrent[currentEntryId]; }
			set { currentEntryId = entriesCurrent.IndexOf(value); }
		}
		TextureHolder NextEntry { get { return entriesCurrent[WrapIndex(currentEntryId + 1)]; } }
		TextureHolder PrevEntry { get { return entriesCurrent[WrapIndex(currentEntryId - 1)]; } }
		TextureHolder NextEntry2 { get { return entriesCurrent[WrapIndex(currentEntryId + 2)]; } }
		TextureHolder PrevEntry2 { get { return entriesCurrent[WrapIndex(currentEntryId - 2)]; } }

		int fadeLevel = 1;
		float[] fadeLevels = { 1f, 0.75f, 0.5f, 0.25f };

		float switchScrollPos = 0f;
		float switchScrollScale = 0f;
		bool enableUIAnimations = true;
		bool enableBackground = true;

		bool dirty = true;

		public static Texture2D txBG, txCircle;

		void AppInit()
		{
			// build list
			if (startingPath.IsDirectory) directory = startingPath;
			else directory = startingPath.Up();

			List<String> names = new List<string>();
			foreach (Path file in directory.Files(p => supportedTypes.Contains(p.Extension), false)) names.Add(file.FileName);
			NumericComparer nc = new NumericComparer();
			names.Sort(nc); // make sure proper order

			foreach (string name in names) entriesOriginal.Add(new TextureHolder(directory.Combine(name), false));
			entriesCurrent = entriesOriginal;

			CurrentEntry = entriesCurrent.Find(p => (p.sourcePath == startingPath));
			if (currentEntryId < 0) currentEntryId = 0; // no negative numbers >:(

			// build gradient
			txBG = ImageHelper.MakeGradient(1, GraphicsDevice.Adapter.CurrentDisplayMode.Height, new Color(0.1f, 0.1f, 0.125f), new Color(0.2f, 0.2f, 0.25f));
			// and circle
			txCircle = ImageHelper.MakeCircle(512);
			// fuzz circle
			for (int i = 0; i < 8; i++) txCircle = ImageHelper.Fuzz(txCircle, 16+i);

			TextureHolder.StartLoadThread();
		}

		void AppUpdate(GameTime gameTime)
		{
			if (entriesCurrent.Count == 0)
			{
				Window.Title = "Stellariview - No images present!";

				if (Input.KeyPressed(Keys.Escape)) Exit();

				return;
			}

			if (lastFrameEntryId != currentEntryId)
			{
				//entriesCurrent[lastFrameEntryId].Unload();
			}
			lastFrameEntryId = currentEntryId;

			#region Processing key input
			bool alt = (Input.KeyHeld(Keys.LeftAlt) || Input.KeyHeld(Keys.RightAlt));
			bool ctrl = (Input.KeyHeld(Keys.LeftControl) || Input.KeyHeld(Keys.RightControl));

			if (alt)
			{
				if (Input.KeyPressed(Keys.Enter))
				{
					//Window.IsBorderless = !Window.IsBorderless;
					ToggleFullscreen();
				}
			}
			else
			{
				if (Input.KeyPressed(Keys.Escape))
				{
					if (graphics.IsFullScreen) ToggleFullscreen();
					else Exit();
				}

				if (Input.KeyPressed(Keys.F11)) ToggleFullscreen();

				if (Input.KeyPressed(Keys.A)) enableUIAnimations = !enableUIAnimations;
				if (Input.KeyPressed(Keys.B)) enableBackground = !enableBackground;

				if (Input.KeyPressed(Keys.S)) Shuffle();
				if (Input.KeyPressed(Keys.F)) fadeLevel = (fadeLevel + 1) % fadeLevels.Length;

				if (Input.KeyPressed(Keys.Left) || Input.KeyPressed(Keys.Z)) { GoPrev(); }
				if (Input.KeyPressed(Keys.Right) || Input.KeyPressed(Keys.X)) { GoNext(); }
			}
			#endregion

			if (dirty)
			{
				TextureHolder.pauseLoad = true;
				entriesCurrent[WrapIndex(currentEntryId - 2)].Load();
				entriesCurrent[WrapIndex(currentEntryId + 2)].Load();
				entriesCurrent[WrapIndex(currentEntryId - 1)].Load();
				entriesCurrent[WrapIndex(currentEntryId + 1)].Load();
				CurrentEntry.Load();
				TextureHolder.pauseLoad = false;

				string title = "Stellariview - " + CurrentEntry.sourcePath.FileName;
				if (entriesCurrent != entriesOriginal) title += " (shuffle)";
				Window.Title = title;

				dirty = false;
			}
		}

		void AppDraw(GameTime gameTime)
		{
			spriteBatch.GraphicsDevice.Clear(Color.Black);

			if (entriesCurrent.Count == 0)
			{
				spriteBatch.GraphicsDevice.Clear(new Color(0.25f, 0f, 0f));
				return;
			}

			float curFadeLevel = fadeLevels[fadeLevel];
			Color fadeColor = new Color(new Vector4(curFadeLevel));
			Color fadeColor2 = new Color(new Vector4(curFadeLevel * curFadeLevel));

			Vector2 screenSize = new Vector2(Window.ClientBounds.Width, Window.ClientBounds.Height);
			Vector2 screenCenter = screenSize * 0.5f;

			const float PER_SECOND = 0.64f;
			const float PER_SECOND_INVERT = 1 - PER_SECOND;
			float proportion = PER_SECOND + (PER_SECOND_INVERT * deltaTimeDraw);
			proportion = Math.Max(0f, Math.Min(proportion, 1f));
			switchScrollScale *= proportion;
			if (!enableUIAnimations) switchScrollScale = 0;

			if (Math.Abs(switchScrollScale) < 0.005f) switchScrollScale = 0;

			Vector2 drawOrigin = screenCenter + new Vector2(switchScrollPos * switchScrollScale, 0);

			Vector2 ces = CurrentEntry.GetSize(screenSize);
			if (ces.X % 2 != screenSize.X % 2) drawOrigin += new Vector2(0.5f, 0f); // enforce clarity on dimensions not matching parity

			spriteBatch.Begin();//SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.Default, RasterizerState.CullNone);
			//spriteBatch.Draw(imgCurrent, Vector2.Zero, Color.White);

			if (enableBackground) spriteBatch.Draw(txBG, Window.ClientBounds, Color.White); // bg

			PrevEntry2.Draw(spriteBatch, drawOrigin - new Vector2(CurrentEntry.GetSize(screenSize).X * 0.5f + PrevEntry.GetSize(screenSize).X + PrevEntry2.GetSize(screenSize).X * 0.5f, 0), screenSize, fadeColor2);
			NextEntry2.Draw(spriteBatch, drawOrigin + new Vector2(CurrentEntry.GetSize(screenSize).X * 0.5f + NextEntry.GetSize(screenSize).X + NextEntry2.GetSize(screenSize).X * 0.5f, 0), screenSize, fadeColor2);

			PrevEntry.Draw(spriteBatch, drawOrigin - new Vector2(CurrentEntry.GetSize(screenSize).X * 0.5f + PrevEntry.GetSize(screenSize).X * 0.5f, 0), screenSize, fadeColor);
			NextEntry.Draw(spriteBatch, drawOrigin + new Vector2(CurrentEntry.GetSize(screenSize).X * 0.5f + NextEntry.GetSize(screenSize).X * 0.5f, 0), screenSize, fadeColor);

			Vector2 parity = Vector2.Zero;
			if (ces.Y % 2 != screenSize.Y % 2) parity += new Vector2(0f, 0.5f); // enforce clarity on dimensions not matching parity
			CurrentEntry.Draw(spriteBatch, drawOrigin + parity, screenSize);

			// test: view circle
			//spriteBatch.Draw(txCircle, screenCenter, null, Color.White, 0f, Vector2.One * 256f, 1f, SpriteEffects.None, 0f);

			spriteBatch.End();
		}

		void GoPrev()
		{
			Vector2 screenSize = new Vector2(Window.ClientBounds.Width, Window.ClientBounds.Height);

			float curScrollPos = switchScrollPos * switchScrollScale;
			switchScrollPos = curScrollPos - (CurrentEntry.GetSize(screenSize).X * 0.5f + PrevEntry.GetSize(screenSize).X * 0.5f);
			SwitchScroll(screenSize);

			currentEntryId = WrapIndex(currentEntryId - 1);
			dirty = true;
		}
		void GoNext()
		{
			Vector2 screenSize = new Vector2(Window.ClientBounds.Width, Window.ClientBounds.Height);

			float curScrollPos = switchScrollPos * switchScrollScale;
			switchScrollPos = curScrollPos + (CurrentEntry.GetSize(screenSize).X * 0.5f + NextEntry.GetSize(screenSize).X * 0.5f);
			SwitchScroll(screenSize);

			currentEntryId = WrapIndex(currentEntryId + 1);
			dirty = true;
		}
		void SwitchScroll(Vector2 screenSize)
		{
			switchScrollPos = Math.Max(-screenSize.X, Math.Min(switchScrollPos, screenSize.X)); // clamp to reasonable value so the screen doesn't get left behind
			switchScrollScale = 1f;

			if (switchScrollPos != 0)
			{
				while (Math.Abs(switchScrollPos) < 128)
				{
					switchScrollPos *= 2f;
					switchScrollScale /= 2f;
				}
			}
		}

		void WrapIndex() { currentEntryId = WrapIndex(currentEntryId); }
		int WrapIndex(int index)
		{
			while (index < 0) index += entriesCurrent.Count;
			while (index >= entriesCurrent.Count) index -= entriesCurrent.Count;
			return index;
		}

		void Shuffle()
		{
			TextureHolder current = CurrentEntry;

			if (entriesCurrent != entriesOriginal) entriesCurrent = entriesOriginal;
			else
			{
				Random rand = new Random();

				entriesCurrent = new List<TextureHolder>();
				foreach (TextureHolder entry in entriesOriginal)
				{
					entriesCurrent.Insert(rand.Next(entriesCurrent.Count), entry);
				}
			}

			CurrentEntry = current;

			dirty = true;
		}

		/*TextureHolder LoadImage(string name)
		{
			return new TextureHolder(directory.Combine(name));
		}*/
	}
}