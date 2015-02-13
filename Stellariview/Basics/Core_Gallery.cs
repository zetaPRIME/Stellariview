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

		int fadeLevel = 3;
		float[] fadeLevels = { 1f, 0.75f, 0.5f, 0.25f };

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

				if (Input.KeyPressed(Keys.S)) Shuffle();
				if (Input.KeyPressed(Keys.F)) fadeLevel = (fadeLevel + 1) % fadeLevels.Length;

				if (Input.KeyPressed(Keys.Left)) currentEntryId = WrapIndex(currentEntryId - 1);
				if (Input.KeyPressed(Keys.Right)) currentEntryId = WrapIndex(currentEntryId + 1);
			}
			#endregion

			entriesCurrent[WrapIndex(currentEntryId - 2)].Load();
			entriesCurrent[WrapIndex(currentEntryId + 2)].Load();
			entriesCurrent[WrapIndex(currentEntryId - 1)].Load();
			entriesCurrent[WrapIndex(currentEntryId + 1)].Load();
			CurrentEntry.Load();

			string title = "Stellariview - " + CurrentEntry.sourcePath.FileName;
			if (entriesCurrent != entriesOriginal) title += " (shuffle)";
			Window.Title = title;
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

			spriteBatch.Begin();//SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.Default, RasterizerState.CullNone);
			//spriteBatch.Draw(imgCurrent, Vector2.Zero, Color.White);

			PrevEntry2.Draw(spriteBatch, screenCenter - new Vector2(CurrentEntry.GetSize(screenSize).X * 0.5f + PrevEntry.GetSize(screenSize).X + PrevEntry2.GetSize(screenSize).X * 0.5f, 0), screenSize, fadeColor2);
			NextEntry2.Draw(spriteBatch, screenCenter + new Vector2(CurrentEntry.GetSize(screenSize).X * 0.5f + NextEntry.GetSize(screenSize).X + NextEntry2.GetSize(screenSize).X * 0.5f, 0), screenSize, fadeColor2);

			PrevEntry.Draw(spriteBatch, screenCenter - new Vector2(CurrentEntry.GetSize(screenSize).X * 0.5f + PrevEntry.GetSize(screenSize).X * 0.5f, 0), screenSize, fadeColor);
			NextEntry.Draw(spriteBatch, screenCenter + new Vector2(CurrentEntry.GetSize(screenSize).X * 0.5f + NextEntry.GetSize(screenSize).X * 0.5f, 0), screenSize, fadeColor);

			CurrentEntry.Draw(spriteBatch, screenCenter, screenSize);

			spriteBatch.End();
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
		}

		/*TextureHolder LoadImage(string name)
		{
			return new TextureHolder(directory.Combine(name));
		}*/
	}
}