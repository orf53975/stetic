using Gtk;
using System;
using System.Collections;

namespace Stetic {

	public class Project : IStetic {
		Hashtable nodes;
		NodeStore store;

		public Project ()
		{
			nodes = new Hashtable ();
			store = new NodeStore (typeof (ProjectNode));
		}

		public void AddWindow (Stetic.Wrapper.Window window)
		{
			AddWindow (window, false);
		}

		public void AddWindow (Stetic.Wrapper.Window window, bool select)
		{
			AddWidget (window.Wrapped, null, -1);
			if (select)
				Selection = window.Wrapped;
		}

		void AddWidget (Widget widget, ProjectNode parent)
		{
			AddWidget (widget, parent, -1);
		}

		void AddWidget (Widget widget, ProjectNode parent, int position)
		{
			Stetic.Wrapper.Widget wrapper = Stetic.Wrapper.Widget.Lookup (widget);
			if (wrapper == null)
				return;

			ProjectNode node = new ProjectNode (wrapper);
			nodes[widget] = node;
			if (parent == null) {
				if (position == -1)
					store.AddNode (node);
				else
					store.AddNode (node, position);
			} else {
				if (position == -1)
					parent.AddChild (node);
				else
					parent.AddChild (node, position);
			}
			widget.Destroyed += WidgetDestroyed;

			parent = node;

			Stetic.Wrapper.Container container = Stetic.Wrapper.Container.Lookup (widget);
			if (container != null) {
				container.ContentsChanged += ContentsChanged;
				foreach (Gtk.Widget w in container.RealChildren)
					AddWidget (w, parent);
			}
		}

		void UnhashNodeRecursive (ProjectNode node)
		{
			nodes.Remove (node.Widget);
			for (int i = 0; i < node.ChildCount; i++)
				UnhashNodeRecursive (node[i] as ProjectNode);
		}

		void RemoveNode (ProjectNode node)
		{
			UnhashNodeRecursive (node);

			ProjectNode parent = node.Parent as ProjectNode;
			if (parent == null)
				store.RemoveNode (node);
			else
				parent.RemoveChild (node);
		}

		void WidgetDestroyed (object obj, EventArgs args)
		{
			ProjectNode node = nodes[obj] as ProjectNode;
			if (node != null)
				RemoveNode (node);
		}

		void ContentsChanged (Stetic.Wrapper.Container cwrap)
		{
			Container container = cwrap.Wrapped as Container;
			ProjectNode node = nodes[container] as ProjectNode;
			if (node == null)
				return;

			ArrayList children = new ArrayList ();
			foreach (Gtk.Widget w in cwrap.RealChildren) {
				if (w != null)
					children.Add (w);
			}

			int i = 0;
			while (i < node.ChildCount && i < children.Count) {
				Widget widget = children[i] as Widget;
				ITreeNode child = nodes[widget] as ITreeNode;

				if (child == null)
					AddWidget (widget, node, i);
				else if (child != node[i]) {
					int index = node.IndexOf (child);
					while (index > i) {
						RemoveNode (node[i] as ProjectNode);
						index--;
					}
				}
				i++;
			}

			while (i < node.ChildCount)
				RemoveNode (node[i] as ProjectNode);

			while (i < children.Count)
				AddWidget (children[i++] as Widget, node);
		}

		public IEnumerable Toplevels {
			get {
				ArrayList list = new ArrayList ();
				foreach (Widget w in nodes.Keys) {
					if (w is Gtk.Window)
						list.Add (w);
				}
				return list;
			}
		}

		public void DeleteWindow (Widget window)
		{
			store.RemoveNode (nodes[window] as ProjectNode);
			nodes.Remove (window);
		}

		public NodeStore Store {
			get {
				return store;
			}
		}

		public void Clear ()
		{
			nodes.Clear ();
			store = new NodeStore (typeof (ProjectNode));
		}

		public delegate void SelectedHandler (Stetic.Wrapper.Widget focus, ProjectNode node);
		public event SelectedHandler Selected;

		Gtk.Widget selection;

		// IStetic

		public Gtk.Widget Selection
		{
			get {
				return selection;
			}
			set {
				if (selection == value)
					return;

				if (selection != null) {
					Stetic.Wrapper.Container parent = Stetic.Wrapper.Container.LookupParent (selection);
					if (parent != null)
						parent.UnSelect (selection);
				}

				selection = value;

				if (Selected == null)
					return;

				Stetic.Wrapper.Widget wrapper = Stetic.Wrapper.Widget.Lookup (selection);
				if (wrapper == null)
					Selected (null, null);
				else
					Selected (wrapper, nodes[selection] as ProjectNode);
			}
		}

		public void PopupContextMenu (Stetic.Wrapper.Widget wrapper)
		{
			Gtk.Menu m = new ContextMenu (wrapper);
			m.Popup ();
		}

		public void PopupContextMenu (Placeholder ph)
		{
			Gtk.Menu m = new ContextMenu (ph);
			m.Popup ();
		}

		public Gtk.Widget LookupWidgetById (string id)
		{
			foreach (Gtk.Widget w in nodes.Keys) {
				if (w.Name == id)
					return w;
			}
			return null;
		}

		public event ISteticDelegate GladeImportComplete;

		public void BeginGladeImport ()
		{
			;
		}

		public void EndGladeImport ()
		{
			if (GladeImportComplete != null)
				GladeImportComplete ();
		}
	}

	[TreeNode (ColumnCount=2)]
	public class ProjectNode : TreeNode {
		Stetic.Wrapper.Widget wrapper;
		Gdk.Pixbuf icon;

		public ProjectNode (Stetic.Wrapper.Widget wrapper)
		{
			this.wrapper = wrapper;
			icon = Stetic.Palette.IconForType (wrapper.GetType ());
		}

		public Stetic.Wrapper.Widget Wrapper {
			get {
				return wrapper;
			}
		}

		public Widget Widget {
			get {
				return (Gtk.Widget)wrapper.Wrapped;
			}
		}

		[TreeNodeValue (Column=0)]
		public Gdk.Pixbuf Icon {
			get {
				return icon;
			}
		}

		[TreeNodeValue (Column=1)]
		public string Name {
			get {
				return Widget.Name;
			}
		}

		public override string ToString ()
		{
			return "[ProjectNode " + GetHashCode().ToString() + " " + Widget.GetType().FullName + " '" + Name + "']";
		}
	}


}
