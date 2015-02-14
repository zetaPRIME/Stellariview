using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;

using Fluent.IO;
using Ionic.Zip;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace Stellariview
{
	//

	public partial class Core : Game
	{
		public static Core instance;
		//public static EngineMode mode = EngineMode.Game;
		public static SpriteBatch spriteBatch;
		public GraphicsDeviceManager graphics;

		public static object gfxLock = new object();
		public static GameTime frameTime;
		public static float frameTimeTotal;

		public static float deltaTime, deltaTimeDraw;

		public static SpriteFont fontDebug;
		public static Texture2D txPixel;

		public static RenderTarget2D screenTarget;

		// for command line things
		public static string forceScene = "";

		// flags
		public static bool debugDisplay = false;

		public DateTime initStart;

		public Core()
		{
			graphics = new GraphicsDeviceManager(this);
			Content.RootDirectory = "Content/Native";
		}

		protected override void Initialize()
		{
			initStart = DateTime.Now;

			573.ToString(); // because why not

			// making sure of a few things
			IsFixedTimeStep = false;
			TargetElapsedTime = TimeSpan.FromSeconds(1f / 60f);

			graphics.PreferredBackBufferWidth = 854;
			graphics.PreferredBackBufferHeight = 480;
			graphics.IsFullScreen = false;

			graphics.ApplyChanges();

			this.IsMouseVisible = true;
			Window.AllowUserResizing = true;

			Thread.CurrentThread.Priority = ThreadPriority.AboveNormal; // prioritize UI thread over load thread; should not impact load thread much as UI is comparatively light

			base.Initialize();
		}

		protected override void LoadContent()
		{
			spriteBatch = new SpriteBatch(graphics.GraphicsDevice);

			//fontDebug = Content.Load<SpriteFont>("DebugFont");
			txPixel = new Texture2D(graphics.GraphicsDevice, 1, 1);
			txPixel.SetData<Color>(new Color[] { Color.White });

			Input.Init();
			Window.TextInput += Input.OnTextInput;
		}

		bool init = false;
		protected override void Update(GameTime gameTime)
		{
			if (!init)
			{
				//GameState.SetGameSize((int)GameDef.screenSize.X, (int)GameDef.screenSize.Y);

				AppInit();

				TimeSpan span = DateTime.Now - initStart;
				Console.WriteLine("Init took " + span.TotalSeconds + " seconds");
				init = true;
			}
			//Console.WriteLine("Resolution is " + Window.ClientBounds.Width + "x" + Window.ClientBounds.Height);

			if (assertWindowSize)
			{
				SetWindowSize(boundsBeforeFullscreen.Width, boundsBeforeFullscreen.Height);
				assertWindowSize = false;
			}

			Input.Update();

			AppUpdate(gameTime);

			base.Update(gameTime);
		}

		protected override void Draw(GameTime gameTime)
		{
			float prevFrameTime = frameTimeTotal;
			float thisFrameTime = (float)gameTime.TotalGameTime.TotalSeconds;
			//if (thisFrameTime - prevFrameTime < 1f / 60f) return; // don't need more than 60fps
			deltaTimeDraw = thisFrameTime - prevFrameTime;

			frameTime = gameTime;
			frameTimeTotal = thisFrameTime;

			lock (gfxLock)
			{
				TextureHolder.ProcessConvertQueue();

				spriteBatch.GraphicsDevice.SetRenderTarget(null);

				AppDraw(gameTime);

				base.Draw(gameTime);
			}
		}

		internal void PrepareTarget()
		{
			if (screenTarget == null || screenTarget.Bounds != Window.ClientBounds)
			{
				screenTarget = new RenderTarget2D(GraphicsDevice, Window.ClientBounds.Width, Window.ClientBounds.Height, false,
					GraphicsDevice.PresentationParameters.BackBufferFormat, GraphicsDevice.PresentationParameters.DepthStencilFormat, 0, RenderTargetUsage.PreserveContents);
			}
			GraphicsDevice.SetRenderTarget(screenTarget);
		}

		internal void BakeToScreen()
		{
			spriteBatch.End();
			spriteBatch.GraphicsDevice.SetRenderTarget(null);
			spriteBatch.Begin();
			spriteBatch.Draw(screenTarget, Vector2.Zero, Color.White);
			spriteBatch.End();
		}


		Rectangle boundsBeforeFullscreen;
		bool assertWindowSize = false;
		void ToggleFullscreen()
		{
			bool isFullScreen = graphics.IsFullScreen;

			DisplayMode dispMode = graphics.GraphicsDevice.Adapter.CurrentDisplayMode;

			if (!isFullScreen)
			{
				boundsBeforeFullscreen = Window.ClientBounds;
				graphics.PreferredBackBufferWidth = dispMode.Width;
				graphics.PreferredBackBufferHeight = dispMode.Height;
				graphics.ToggleFullScreen();
				graphics.ApplyChanges();

				IsMouseVisible = false;
			}
			else
			{
				graphics.PreferredBackBufferWidth = boundsBeforeFullscreen.Width;
				graphics.PreferredBackBufferHeight = boundsBeforeFullscreen.Height;
				graphics.ToggleFullScreen();
				graphics.ApplyChanges();

				IsMouseVisible = true;

				assertWindowSize = true;
				//SetWindowSize(boundsBeforeFullscreen.Width, boundsBeforeFullscreen.Height);
			}
		}

		void SetWindowSize(int width, int height)
		{
			Type otkgw = typeof(OpenTKGameWindow);
			FieldInfo wfield = otkgw.GetField("window", BindingFlags.NonPublic | BindingFlags.Instance);
			OpenTK.GameWindow wnd = (OpenTK.GameWindow)wfield.GetValue(Core.instance.Window);

			wnd.Width = width; wnd.Height = height;
		}
	}
}
