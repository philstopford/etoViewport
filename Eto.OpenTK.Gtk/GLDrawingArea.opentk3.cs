#if OPENTK3
////////////////////////////////////////////////////////////////////////////////
// Gtk GLWidget Sharp - Gtk OpenGL Widget for CSharp using OpenTK
////////////////////////////////////////////////////////////////////////////////
/*
Usage:
    To render either override OnRenderFrame() or hook to the RenderFrame event.

    When GraphicsContext.ShareContexts == True (Default)
    To setup OpenGL state hook to the following events:
        GLWidget.GraphicsContextInitialized
        GLWidget.GraphicsContextShuttingDown

    When GraphicsContext.ShareContexts == False
    To setup OpenGL state hook to the following events:
        GLWidget.Initialized
        GLWidget.ShuttingDown 
*/
////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using Eto.Drawing;
using Eto.GtkSharp;
using Gtk;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Platform;

namespace Eto.OpenTK.Gtk
{
	[ToolboxItem (true)]
	public class GLDrawingArea : DrawingArea, IDisposable
	{
		IGraphicsContext graphicsContext;
		static int graphicsContextCount;

        const string macos_libgdk_name = "libgdk-quartz-2.0.0.dylib";
		const string linux_libgdk_x11_name = "libgdk-3.so.0";
		const string libgdk_name = "libgdk-win32-2.0-0.dll";

        const string linux_libx11_name = "libX11.so.6";
		const string linux_libgl_name = "libGL.so.1";
		const string libX11_name = "libX11";

		/// <summary>Use a single buffer versus a double buffer.</summary>
		[Browsable (true)]
		public bool SingleBuffer { get; set; }

		/// <summary>Color Buffer Bits-Per-Pixel</summary>
		public int ColorBPP { get; set; }

		/// <summary>Accumulation Buffer Bits-Per-Pixel</summary>
		public int AccumulatorBPP { get; set; }

		/// <summary>Depth Buffer Bits-Per-Pixel</summary>
		public int DepthBPP { get; set; }

		/// <summary>Stencil Buffer Bits-Per-Pixel</summary>
		public int StencilBPP { get; set; }

		/// <summary>Number of samples</summary>
		public int Samples { get; set; }

		/// <summary>Indicates if steropic renderering is enabled</summary>
		public bool Stereo { get; set; }

		IWindowInfo windowInfo;

		/// <summary>The major version of OpenGL to use.</summary>
		public int GlVersionMajor { get; set; }

		/// <summary>The minor version of OpenGL to use.</summary>
		public int GlVersionMinor { get; set; }

		private Size size;

		/// <summary>
		/// Gets or sets the context size.
		/// </summary>
		/// <value>The width.</value>
		public virtual Size GLSize {
			get {
				return Visible ? Allocation.Size.ToEto () : size; 
			}
			set {
				if (size != value) {
					size = value;
					var alloc = Allocation;
					alloc.Size = value.ToGdk ();
					SetSizeRequest (size.Width, size.Height);
				}
			}
		}

		bool initialized = false;

		public virtual bool IsInitialized {
			get { return initialized; }
		}

		public GraphicsContextFlags GraphicsContextFlags {
			get { return graphicsContextFlags; }
			set { graphicsContextFlags = value; }
		}

		GraphicsContextFlags graphicsContextFlags;

		/// <summary>Constructs a new GLWidget.</summary>
		public GLDrawingArea () : this (GraphicsMode.Default)
		{
		}

		/// <summary>Constructs a new GLWidget using a given GraphicsMode</summary>
		public GLDrawingArea (GraphicsMode graphicsMode) : this (graphicsMode, 3, 0, GraphicsContextFlags.Default)
		{
		}

		/// <summary>Constructs a new GLWidget</summary>
		public GLDrawingArea (GraphicsMode graphicsMode, int glVersionMajor, int glVersionMinor, GraphicsContextFlags graphicsContextFlags)
		{
            this.DoubleBuffered = false;

			CanFocus = true;
			CanDefault = true;

			SingleBuffer = graphicsMode.Buffers == 1;
			ColorBPP = graphicsMode.ColorFormat.BitsPerPixel;
			AccumulatorBPP = graphicsMode.AccumulatorFormat.BitsPerPixel;
			DepthBPP = graphicsMode.Depth;
			StencilBPP = graphicsMode.Stencil;
			Samples = graphicsMode.Samples;
			Stereo = graphicsMode.Stereo;
            
			GlVersionMajor = glVersionMajor;
			GlVersionMinor = glVersionMinor;
			GraphicsContextFlags = graphicsContextFlags;

			Events |= Gdk.EventMask.ExposureMask;

			Drawn += HandleDrawn;
        }

#if GTK2
        ~GLDrawingArea ()
		{
			Dispose (false);
		}

		public override void Dispose ()
		{
			GC.SuppressFinalize (this);
			Dispose (true);
			base.Dispose ();
		}

		protected virtual void Dispose (bool disposing)
		{
			if (disposing) {
				Cleanup();
			}
		}

#else
		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				Cleanup();
			}
		}
#endif
		void Cleanup()
		{
			graphicsContext.MakeCurrent (windowInfo);
			OnShuttingDown ();
			if (GraphicsContext.ShareContexts && (Interlocked.Decrement (ref graphicsContextCount) == 0)) {
				OnGraphicsContextShuttingDown ();
				sharedContextInitialized = false;
			}
			graphicsContext.Dispose ();
		}

		public virtual void MakeCurrent ()
		{
			if (!initialized) {
				return;
			}

			graphicsContext.MakeCurrent (windowInfo);
		}

		public virtual void SwapBuffers ()
		{
			if (!initialized) {
				return;
			}
                       
			Display.Flush ();
			graphicsContext.SwapBuffers ();
			Display.Sync ();           
		}


		// Called when the first GraphicsContext is created in the case of GraphicsContext.ShareContexts == True;
		public static event EventHandler GraphicsContextInitialized;

		static void OnGraphicsContextInitialized ()
		{
			GraphicsContextInitialized?.Invoke (null, EventArgs.Empty);
		}

		// Called when the first GraphicsContext is being destroyed in the case of GraphicsContext.ShareContexts == True;
		public static event EventHandler GraphicsContextShuttingDown;

		static void OnGraphicsContextShuttingDown ()
		{
			GraphicsContextShuttingDown?.Invoke (null, EventArgs.Empty);
		}

		// Called when this GLWidget has a valid GraphicsContext
		public event EventHandler Initialized;

		protected virtual void OnInitialized ()
		{
			Initialized?.Invoke (this, EventArgs.Empty);
		}

		// Called when this GLWidget needs to render a frame
		public event EventHandler Draw;

		protected virtual void OnDraw ()
		{
			Draw?.Invoke (this, EventArgs.Empty);
		}

		// Called when this GLWidget is being Disposed
		public event EventHandler ShuttingDown;

		protected virtual void OnShuttingDown ()
		{
			ShuttingDown?.Invoke (this, EventArgs.Empty);
		}

		static bool sharedContextInitialized = false;

		void InitializeContext()
		{
			Toolkit.Init();

			// If this looks uninitialized...  initialize.
			if (ColorBPP == 0) {
				ColorBPP = 24;

				if (DepthBPP == 0)
					DepthBPP = 16;
			}

			ColorFormat colorBufferColorFormat = new ColorFormat (ColorBPP);

			ColorFormat accumulationColorFormat = new ColorFormat (AccumulatorBPP);

			int buffers = 2;
			if (SingleBuffer)
				buffers--;

			var graphicsMode = new GraphicsMode (colorBufferColorFormat, DepthBPP, StencilBPP, Samples, accumulationColorFormat, buffers, Stereo);

			// IWindowInfo
			if (Configuration.RunningOnWindows) {
				IntPtr windowHandle = gdk_win32_drawable_get_handle (GdkWindow.Handle);
				windowInfo = Utilities.CreateWindowsWindowInfo (windowHandle);
			} else if (Configuration.RunningOnMacOS) {
                /* doesn't seem to work.. takes up whole window for now, but better than just crashing?
                if (!gdk_window_ensure_native(GdkWindow.Handle))
                    throw new InvalidOperationException("Couldn't create native NSView");
                */

                IntPtr windowHandle = gdk_quartz_window_get_nswindow(GdkWindow.Handle);
                
                IntPtr viewHandle = gdk_quartz_window_get_nsview (GdkWindow.Handle);
				windowInfo = Utilities.CreateMacOSWindowInfo(windowHandle, viewHandle);
			} else if (Configuration.RunningOnX11) {
				// Display

				IntPtr display = gdk_x11_display_get_xdisplay(Display.Handle);
				int screen = Screen.Number;
				IntPtr windowHandle = gdk_x11_window_get_xid(Window.Handle);
				IntPtr rootWindow = gdk_x11_window_get_xid(RootWindow.Handle);

				IntPtr visualInfo;
				if (graphicsMode.Index.HasValue) {
					XVisualInfo info = new XVisualInfo ();
					info.VisualID = graphicsMode.Index.Value;
					int dummy;
					visualInfo = XGetVisualInfo (display, XVisualInfoMask.ID, ref info, out dummy);
				} else {
					visualInfo = GetVisualInfo (display);
				}

				windowInfo = Utilities.CreateX11WindowInfo (display, screen, windowHandle, rootWindow, visualInfo);
				XFree (visualInfo);
			} else
				throw new PlatformNotSupportedException ();

			// GraphicsContext
			graphicsContext = new GraphicsContext (graphicsMode, windowInfo, GlVersionMajor, GlVersionMinor, graphicsContextFlags);
			graphicsContext.MakeCurrent (windowInfo);

			if (GraphicsContext.ShareContexts) {
				Interlocked.Increment (ref graphicsContextCount);

				if (!sharedContextInitialized) {
					sharedContextInitialized = true;
					((IGraphicsContextInternal)graphicsContext).LoadAll ();
					OnGraphicsContextInitialized ();
				}
			} else {
				((IGraphicsContextInternal)graphicsContext).LoadAll ();
				OnGraphicsContextInitialized ();
			}

			initialized = true;
			OnInitialized ();
			//QueueDraw();
		}

		[GLib.ConnectBefore]

		protected virtual void HandleDrawn(object o, global::Gtk.DrawnArgs args)
		{
			if (!initialized)
            {
				global::Gtk.Application.Invoke((sender, e) => InitializeContext());
				return;
			}

			MakeCurrent();

            if (Configuration.RunningOnMacOS)
            {
                //GL.Viewport(Allocation.Left, Allocation.Top, Allocation.Width, Allocation.Height);
                //GL.MatrixMode(MatrixMode.Projection);
                //GL.LoadIdentity();
                //GL.Ortho(-1.0, 1.0, -1.0, 1.0, 0.0, 4.0);
            }

            OnDraw();
        }

		// protected override bool OnConfigureEvent (Gdk.EventConfigure evnt)
		// {
		// 	bool result = base.OnConfigureEvent (evnt);
		// 	graphicsContext?.Update (windowInfo);
		// 	return result;
		// }

		public enum XVisualClass : int
		{
			StaticGray = 0,
			GrayScale = 1,
			StaticColor = 2,
			PseudoColor = 3,
			TrueColor = 4,
			DirectColor = 5,
		}

		[StructLayout (LayoutKind.Sequential)]
		struct XVisualInfo
		{
			public IntPtr Visual;
			public IntPtr VisualID;
			public int Screen;
			public int Depth;
			public XVisualClass Class;
			public long RedMask;
			public long GreenMask;
			public long blueMask;
			public int ColormapSize;
			public int BitsPerRgb;

			public override string ToString ()
			{
				return String.Format ("id ({0}), screen ({1}), depth ({2}), class ({3})",
					VisualID, Screen, Depth, Class);
			}
		}

		[Flags]
		internal enum XVisualInfoMask
		{
			No = 0x0,
			ID = 0x1,
			Screen = 0x2,
			Depth = 0x4,
			Class = 0x8,
			Red = 0x10,
			Green = 0x20,
			Blue = 0x40,
			ColormapSize = 0x80,
			BitsPerRGB = 0x100,
			All = 0x1FF,
		}

		[DllImport (libX11_name, EntryPoint = "XGetVisualInfo")]
		static extern IntPtr XGetVisualInfoInternal (IntPtr display, IntPtr vinfo_mask, ref XVisualInfo template, out int nitems);

		static IntPtr XGetVisualInfo (IntPtr display, XVisualInfoMask vinfo_mask, ref XVisualInfo template, out int nitems)
		{
			return XGetVisualInfoInternal (display, (IntPtr)(int)vinfo_mask, ref template, out nitems);
		}

		[SuppressUnmanagedCodeSecurity, DllImport (libgdk_name, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr gdk_win32_drawable_get_handle (IntPtr d);

		[SuppressUnmanagedCodeSecurity, DllImport (linux_libx11_name)]
		static extern void XFree (IntPtr handle);

		/// <summary> Returns the X resource (window or pixmap) belonging to a GdkDrawable. </summary>
		/// <remarks> XID gdk_x11_drawable_get_xid(GdkDrawable *drawable); </remarks>
		/// <param name="gdkDisplay"> The GdkDrawable. </param>
		/// <returns> The ID of drawable's X resource. </returns>
		[SuppressUnmanagedCodeSecurity, DllImport (linux_libgdk_x11_name)]
		static extern IntPtr gdk_x11_drawable_get_xid (IntPtr gdkDisplay);

		[SuppressUnmanagedCodeSecurity, DllImport (linux_libgdk_x11_name)]
		private static extern IntPtr gdk_x11_window_get_xid(IntPtr gdkDisplay);
		

		/// <summary> Returns the X display of a GdkDisplay. </summary>
		/// <remarks> Display* gdk_x11_display_get_xdisplay(GdkDisplay *display); </remarks>
		/// <param name="gdkDisplay"> The GdkDrawable. </param>
		/// <returns> The X Display of the GdkDisplay. </returns>
		[SuppressUnmanagedCodeSecurity, DllImport (linux_libgdk_x11_name)]
		static extern IntPtr gdk_x11_display_get_xdisplay (IntPtr gdkDisplay);

		[SuppressUnmanagedCodeSecurity, DllImport (linux_libgl_name)]
		static extern IntPtr glXChooseVisual (IntPtr display, int screen, int[] attr);

        [SuppressUnmanagedCodeSecurity, DllImport(macos_libgdk_name, CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr gdk_quartz_window_get_nsview(IntPtr d);

        [SuppressUnmanagedCodeSecurity, DllImport(macos_libgdk_name, CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr gdk_quartz_window_get_nswindow(IntPtr d);

        [SuppressUnmanagedCodeSecurity, DllImport(macos_libgdk_name, CallingConvention = CallingConvention.Cdecl)]
        static extern bool gdk_window_ensure_native(IntPtr window);

        IntPtr GetVisualInfo (IntPtr display)
		{
			try {
				int[] attributes = AttributeList.ToArray ();
				return glXChooseVisual (display, Screen.Number, attributes);
			} catch (DllNotFoundException e) {
				throw new DllNotFoundException ("OpenGL dll not found!", e);
			} catch (EntryPointNotFoundException enf) {
				throw new EntryPointNotFoundException ("Glx entry point not found!", enf);
			}
		}

		const int GLX_NONE = 0;
		const int GLX_USE_GL = 1;
		const int GLX_BUFFER_SIZE = 2;
		const int GLX_LEVEL = 3;
		const int GLX_RGBA = 4;
		const int GLX_DOUBLEBUFFER = 5;
		const int GLX_STEREO = 6;
		const int GLX_AUX_BUFFERS = 7;
		const int GLX_RED_SIZE = 8;
		const int GLX_GREEN_SIZE = 9;
		const int GLX_BLUE_SIZE = 10;
		const int GLX_ALPHA_SIZE = 11;
		const int GLX_DEPTH_SIZE = 12;
		const int GLX_STENCIL_SIZE = 13;
		const int GLX_ACCUM_RED_SIZE = 14;
		const int GLX_ACCUM_GREEN_SIZE = 15;
		const int GLX_ACCUM_BLUE_SIZE = 16;
		const int GLX_ACCUM_ALPHA_SIZE = 17;

		List<int> AttributeList {
			get {
				List<int> attributeList = new List<int> (24);

				attributeList.Add (GLX_RGBA);

				if (!SingleBuffer)
					attributeList.Add (GLX_DOUBLEBUFFER);

				if (Stereo)
					attributeList.Add (GLX_STEREO);

				attributeList.Add (GLX_RED_SIZE);
				attributeList.Add (ColorBPP / 4); // TODO support 16-bit

				attributeList.Add (GLX_GREEN_SIZE);
				attributeList.Add (ColorBPP / 4); // TODO support 16-bit

				attributeList.Add (GLX_BLUE_SIZE);
				attributeList.Add (ColorBPP / 4); // TODO support 16-bit

				attributeList.Add (GLX_ALPHA_SIZE);
				attributeList.Add (ColorBPP / 4); // TODO support 16-bit

				attributeList.Add (GLX_DEPTH_SIZE);
				attributeList.Add (DepthBPP);

				attributeList.Add (GLX_STENCIL_SIZE);
				attributeList.Add (StencilBPP);

				//attributeList.Add(GLX_AUX_BUFFERS);
				//attributeList.Add(Buffers);

				attributeList.Add (GLX_ACCUM_RED_SIZE);
				attributeList.Add (AccumulatorBPP / 4);// TODO support 16-bit

				attributeList.Add (GLX_ACCUM_GREEN_SIZE);
				attributeList.Add (AccumulatorBPP / 4);// TODO support 16-bit

				attributeList.Add (GLX_ACCUM_BLUE_SIZE);
				attributeList.Add (AccumulatorBPP / 4);// TODO support 16-bit

				attributeList.Add (GLX_ACCUM_ALPHA_SIZE);
				attributeList.Add (AccumulatorBPP / 4);// TODO support 16-bit

				attributeList.Add (GLX_NONE);

				return attributeList;
			}
		}
	}
}
#endif