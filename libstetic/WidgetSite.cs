using Gtk;
using Gdk;
using System;
using System.Collections;

namespace Stetic {

	public delegate void OccupancyChangedHandler (WidgetSite site);

	public class WidgetSite : WidgetBox, IWidgetSite {

		static TargetEntry[] Targets;
		static TargetList TargetList;
		static Gdk.Atom SteticWidgetType;

		static WidgetSite ()
		{
			SteticWidgetType = Gdk.Atom.Intern ("application/x-stetic-widget", false);

			Targets = new TargetEntry[1];
			Targets[0] = new TargetEntry ("application/x-stetic-widget", 0, 0);

			TargetList = new TargetList ();
			TargetList.Add (SteticWidgetType, 0, 0);
		}

		IStetic stetic;

		public WidgetSite (IStetic stetic)
		{
			WidgetFlags |= WidgetFlags.CanFocus;

			this.stetic = stetic;
			emptySize.Width = emptySize.Height = 10;
			Occupancy = SiteOccupancy.Empty;
		}

		public Widget Contents {
			get {
				return Child;
			}
		}

		public IWidgetSite ParentSite {
			get {
				for (Widget w = Parent; ; w = w.Parent) {
					if (w == null)
						return null;
					if (w is IWidgetSite)
						return w as IWidgetSite;
					if (w.Parent == null)
						return WindowSite.LookupSite (w);
				}
			}
		}

		private void ChildContentsChanged (Stetic.Wrapper.Container container)
		{
			if (OccupancyChanged != null)
				OccupancyChanged (this);
		}

		protected override void OnAdded (Widget child)
		{
			base.OnAdded (child);
			Occupancy = SiteOccupancy.Occupied;

			Stetic.Wrapper.Container container = Stetic.Wrapper.Container.Lookup (child);
			if (container != null)
				container.ContentsChanged += ChildContentsChanged;
		}

		protected override void OnRemoved (Widget w)
		{
			Stetic.Wrapper.Container container = Stetic.Wrapper.Container.Lookup (w);
			if (container != null)
				container.ContentsChanged -= ChildContentsChanged;

			if (Occupancy == SiteOccupancy.Occupied)
				Occupancy = SiteOccupancy.Empty;
			base.OnRemoved (w);
		}

		Requisition emptySize;
		public Requisition EmptySize {
			get {
				return emptySize;
			}
			set {
				emptySize = value;
				if (Occupancy == SiteOccupancy.Empty)
					SetSizeRequest (emptySize.Width, emptySize.Height);
			}
		}

		public enum SiteOccupancy { Empty, Occupied, PseudoOccupied };

		private SiteOccupancy state;
		private SiteOccupancy Occupancy {
			get { return state; }
			set {
				state = value;
				switch (state) {
				case SiteOccupancy.Empty:
					SetSizeRequest (emptySize.Width, emptySize.Height);
					Gtk.Drag.DestSet (this, DestDefaults.All,
							  Targets, DragAction.Move);
					if (OccupancyChanged != null)
						OccupancyChanged (this);
					break;

				case SiteOccupancy.Occupied:
					SetSizeRequest (-1, -1);
					if (faults != null && faults.Count > 0)
						Gtk.Drag.DestSet (this, 0, Targets, DragAction.Move);
					else
						Gtk.Drag.DestUnset (this);
					if (OccupancyChanged != null)
						OccupancyChanged (this);
					break;

				case SiteOccupancy.PseudoOccupied:
					SetSizeRequest (Child.ChildRequisition.Width,
							Child.ChildRequisition.Height);
					Gtk.Drag.DestSet (this, DestDefaults.All,
							  Targets, DragAction.Move);
					break;
				}
			}
		}

		public event OccupancyChangedHandler OccupancyChanged;

		public bool Occupied {
			get { return (Occupancy != SiteOccupancy.Empty); }
		}

		public bool HExpandable {
			get {
				if (Occupancy == SiteOccupancy.Empty)
					return true;

				Stetic.Wrapper.Widget child;
				if (Occupancy == SiteOccupancy.PseudoOccupied)
					child = Stetic.Wrapper.Widget.Lookup (dragWidget);
				else
					child = Stetic.Wrapper.Widget.Lookup (Child);

				if (child != null)
					return child.HExpandable;
				else
					return false;
			}
		}

		public bool VExpandable {
			get {
				if (Occupancy == SiteOccupancy.Empty)
					return true;

				Stetic.Wrapper.Widget child;
				if (Occupancy == SiteOccupancy.PseudoOccupied)
					child = Stetic.Wrapper.Widget.Lookup (dragWidget);
				else
					child = Stetic.Wrapper.Widget.Lookup (Child);

				if (child != null)
					return child.VExpandable;
				else
					return false;
			}
		}

		Hashtable faults;

		void SetupFaults ()
		{
			if (faults == null) {
				faults = new Hashtable ();
				stetic.DragBegin += ShowFaults;
				stetic.DragEnd += HideFaults;
			}
			if (Occupancy == SiteOccupancy.Occupied)
				Gtk.Drag.DestSet (this, 0, Targets, DragAction.Move);
		}

		public void AddHFault (object id, int y, int x1, int x2)
		{
			Gdk.Window win = NewWindow (GdkWindow, Gdk.WindowClass.InputOnly);
			win.MoveResize (x1, y - 2, x2 - x1 , 5);
			if (faults == null || faults.Count == 0)
				SetupFaults ();
			faults[id] = win;
		}

		public void AddVFault (object id, int x, int y1, int y2)
		{
			Gdk.Window win = NewWindow (GdkWindow, Gdk.WindowClass.InputOnly);
			win.MoveResize (x - 2, y1, 5, y2 - y1);
			if (faults == null || faults.Count == 0)
				SetupFaults ();
			faults[id] = win;
		}

		public void ClearFaults ()
		{
			if (faults != null) {
				foreach (Gdk.Window win in faults.Values)
					win.Destroy ();
				faults.Clear ();
			}
			if (Occupancy == SiteOccupancy.Occupied)
				Gtk.Drag.DestUnset (this);
		}

		void ShowFaults ()
		{
			foreach (Gdk.Window win in faults.Values)
				win.Show ();
		}

		void HideFaults ()
		{
			foreach (Gdk.Window win in faults.Values)
				win.Hide ();
			OnDragLeave (null, 0);
		}

		object dragFault;
		Gdk.Window splitter;

		void FindFault (int x, int y, out object fault, out Gdk.Window win)
		{
			int wx, wy, width, height, depth;

			fault = null;
			win = null;
			wx = wy = width = height = 0;

			foreach (object id in faults.Keys) {
				win = faults[id] as Gdk.Window;
				win.GetGeometry (out wx, out wy, out width, out height, out depth);
				if (x >= wx && y >= wy && x <= wx + width && y <= wy + height) {
					fault = id;
					return;
				}
			}
		}

		// This is only called when dragging in fault mode, not in
		// placeholder mode.
		protected override bool OnDragMotion (Gdk.DragContext ctx, int x, int y, uint time)
		{
			if (faults == null || faults.Count == 0)
				return false;

			int wx, wy, width, height, depth;
			object match;
			Gdk.Window matchWin;
			
			FindFault (x, y, out match, out matchWin);

			// If there's a splitter visible, and we're not currently dragging
			// in the fault that owns that splitter, hide it
			if (splitter != null && dragFault != match) {
				splitter.Hide ();
				splitter.Destroy ();
				splitter = null;
			}

			if (dragFault != match) {
				dragFault = match;
				if (dragFault == null)
					return false;

				splitter = NewWindow (GdkWindow, Gdk.WindowClass.InputOutput);
				matchWin.GetGeometry (out wx, out wy, out width, out height, out depth);
				splitter.MoveResize (wx, wy, width, height);
				splitter.ShowUnraised ();
				GdkWindow.Lower ();
			}

			Gdk.Drag.Status (ctx, Gdk.DragAction.Move, time);
			return true;
		}

		protected override void OnDragLeave (Gdk.DragContext ctx, uint time)
		{
			if (splitter != null) {
				splitter.Hide ();
				splitter.Destroy ();
				splitter = null;
				dragFault = null;
			}
		}

		protected int clickX, clickY;
		protected override bool OnButtonPressEvent (Gdk.EventButton evt)
		{
			if (base.OnButtonPressEvent (evt))
				return true;

			clickX = (int)evt.XRoot;
			clickY = (int)evt.YRoot;
			GrabFocus ();
			return true;
		}

		protected Widget dragWidget;

		protected virtual bool StartDrag (Gdk.EventMotion evt)
		{
			if (evt.Window != HandleWindow)
				return false;

			dragWidget = Child;
			if (dragWidget == null)
				return false;

			Occupancy = SiteOccupancy.PseudoOccupied;
			Remove (dragWidget);
			return true;
		}

		protected override bool OnMotionNotifyEvent (Gdk.EventMotion evt)
		{
			Gtk.Window dragWin;
			DragContext ctx;
			int mx, my;
			Requisition req;

			if ((evt.State & ModifierType.Button1Mask) == 0)
				return true;
			if (!Gtk.Drag.CheckThreshold (this, clickX, clickY, (int)evt.XRoot, (int)evt.YRoot))
				return true;

			if (!StartDrag (evt))
				return true;

			stetic.DragBegun ();

			mx = (int)evt.XRoot;
			my = (int)evt.YRoot;

			dragWin = new Gtk.Window (Gtk.WindowType.Popup);
			dragWin.Add (dragWidget);

			req = dragWidget.SizeRequest ();
			if (req.Width < 20 && req.Height < 20)
				dragWin.SetSizeRequest (20, 20);
			else if (req.Width < 20)
				dragWin.SetSizeRequest (20, -1);
			else if (req.Height < 20)
				dragWin.SetSizeRequest (-1, 20);

			dragWin.Move (mx, my);
			dragWin.Show ();

			ctx = Gtk.Drag.Begin (this, TargetList, DragAction.Move, 1, evt);
			Gtk.Drag.SetIconWidget (ctx, dragWin, 0, 0);

			return false;
		}

		public delegate void DropOnHandler (Widget w, object faultId);
		public event DropOnHandler DropOn;

		protected virtual void Drop (Widget w, int x, int y)
		{
			if (faults != null && DropOn != null) {
				object faultId = null;
				Gdk.Window win;
				FindFault (x, y, out faultId, out win);
				DropOn (w, faultId);
			} else
				Add (w);
		}

		protected override bool OnDragDrop (DragContext ctx,
						    int x, int y, uint time)
		{
			WidgetSite source;
			Widget dragged;
			Container parent;

			source = Gtk.Drag.GetSourceWidget (ctx) as WidgetSite;
			if (source == null) {
				Gtk.Drag.Finish (ctx, false, false, time);
				return false;
			}

			dragged = source.dragWidget;
			source.dragWidget = null;
			if (dragged == null) {
				Gtk.Drag.Finish (ctx, false, false, time);
				return false;
			}

			parent = dragged.Parent as Container;
			if (parent != null)
				parent.Remove (dragged);

			Drop (dragged, x, y);
			GrabFocus ();
			Gtk.Drag.Finish (ctx, true, false, time);
			return true;
		}

		protected override void OnDragEnd (DragContext ctx)
		{
			if (dragWidget != null) {
				Container parent;

				parent = dragWidget.Parent as Container;
				if (parent != null)
					parent.Remove (dragWidget);
				Drop (dragWidget, -1, -1);
			} else if (Child == null)
				Occupancy = SiteOccupancy.Empty;

			dragWidget = null;
			stetic.DragEnded ();
		}

		protected override bool OnKeyReleaseEvent (Gdk.EventKey evt)
		{
			if (evt.Key == Gdk.Key.Delete) {
				Delete ();
				return true;
			}
			return false;
		}

		public event EventHandler Selected;

		public void Focus ()
		{
			ShowHandles = true;
			if (Selected != null)
				Selected (this, EventArgs.Empty);
		}

		public void UnFocus ()
		{
			ShowHandles = false;
		}

		public void Select ()
		{
			GrabFocus ();
		}

		public void UnSelect ()
		{
			UnFocus ();
		}

		public void Delete ()
		{
			if (Child != null) {
				Remove (Child);
				if (OccupancyChanged != null)
					OccupancyChanged (this);
			}
		}

	}
}
