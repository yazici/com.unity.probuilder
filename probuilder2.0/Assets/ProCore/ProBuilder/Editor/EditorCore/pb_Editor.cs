#pragma warning disable 0168	///< Disable unused var (that exception hack)

#if UNITY_4_3 || UNITY_4_3_0 || UNITY_4_3_1 || UNITY_4_3_2 || UNITY_4_3_3 || UNITY_4_3_4 || UNITY_4_3_5 || UNITY_4_3_6 || UNITY_4_3_7 || UNITY_4_3_8 || UNITY_4_3_9 || UNITY_4_4 || UNITY_4_4_0 || UNITY_4_4_1 || UNITY_4_4_2 || UNITY_4_4_3 || UNITY_4_4_4 || UNITY_4_4_5 || UNITY_4_4_6 || UNITY_4_4_7 || UNITY_4_4_8 || UNITY_4_4_9 || UNITY_4_5 || UNITY_4_5_0 || UNITY_4_5_1 || UNITY_4_5_2 || UNITY_4_5_3 || UNITY_4_5_4 || UNITY_4_5_5 || UNITY_4_5_6 || UNITY_4_5_7 || UNITY_4_5_8 || UNITY_4_5_9 || UNITY_4_6 || UNITY_4_6_0 || UNITY_4_6_1 || UNITY_4_6_2 || UNITY_4_6_3 || UNITY_4_6_4 || UNITY_4_6_5 || UNITY_4_6_6 || UNITY_4_6_7 || UNITY_4_6_8 || UNITY_4_6_9 || UNITY_4_7 || UNITY_4_7_0 || UNITY_4_7_1 || UNITY_4_7_2 || UNITY_4_7_3 || UNITY_4_7_4 || UNITY_4_7_5 || UNITY_4_7_6 || UNITY_4_7_7 || UNITY_4_7_8 || UNITY_4_7_9 || UNITY_4_8 || UNITY_4_8_0 || UNITY_4_8_1 || UNITY_4_8_2 || UNITY_4_8_3 || UNITY_4_8_4 || UNITY_4_8_5 || UNITY_4_8_6 || UNITY_4_8_7 || UNITY_4_8_8 || UNITY_4_8_9 || UNITY_5_0
#define UNITY_4_3
#define UNITY_4
#endif

#if UNITY_4_0 || UNITY_4_0_1 || UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_3_0 || UNITY_4_3_1 || UNITY_4_3_2 || UNITY_4_3_3 || UNITY_4_3_4 || UNITY_4_3_5
#define UNITY_4
#endif

// Edge graphics aren't up to par yet
#undef UNITY_4

// TODO
// udpate preferences version

using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using ProCore.Common;
using ProBuilder2.Common;
using ProBuilder2.EditorCommon;
using ProBuilder2.MeshOperations;
using ProBuilder2.Math;
using ProBuilder2.GUI;

#if PB_DEBUG
using Parabox.Debug;
#endif

[InitializeOnLoad]
public class pb_Editor : EditorWindow
{

#region STATIC REFERENCES

	public static pb_Editor instance { get { return me; } }
#endregion

#region MENU TEXTURES
	
	GUIContent[] SelectionIcons;
	Texture2D eye_on, eye_off;
#endregion

#region CONSTANT & GUI MEMEBRS
	
	// because editor prefs can change, or shortcuts may be added, certain EditorPrefs need to be force reloaded.
	// adding to this const will force update on updating packages.
	const int EDITOR_PREF_VERSION = 129;

	const string SHARED_GUI = "Assets/6by7/Shared/GUI";
	const float HANDLE_DRAW_DISTANCE = 15f;
	
	const int WINDOW_WIDTH_FlOATING = 102;
	const int WINDOW_WIDTH_DOCKABLE = 105;

	const int SELECT_MODE_LENGTH = 3;

	Color TOOL_SETTINGS_COLOR = EditorGUIUtility.isProSkin ? Color.green : new Color(.2f, .2f, .2f, .2f);

	// GUIStyle pbStyle;
	GUIStyle VertexTranslationInfoStyle;
	// private int UNITY_VERSION;
	GUIStyle eye_style;
#endregion

#region DEBUG

	#if SVN_EXISTS && !RELEASE
	string revisionNo = " no svn found";
	#endif

	#if PB_DEBUG
	pb_Profiler profiler = new pb_Profiler();
	#endif
#endregion

#region LOCAL MEMBERS && EDITOR PREFS
	
	private static pb_Editor me;

	MethodInfo IntersectRayMesh;
	MethodInfo findNearestVertex;

	public EditLevel editLevel;
	private EditLevel previousEditLevel;
	
	public SelectMode selectionMode { get; private set; }
	private SelectMode previousSelectMode;

	public HandleAlignment handleAlignment { get; private set; }
	#if !PROTOTYPE
	private HandleAlignment previousHandleAlignment;
	#endif

	pb_Shortcut[] shortcuts;

	private bool vertexSelectionMask = true;	///< If true, in EditMode.ModeBased && SelectionMode.Vertex only vertices will be selected when dragging.
	public float drawVertexNormals = 0f;
	public bool drawFaceNormals = false;

	private bool limitFaceDragCheckToSelection = true;
	internal bool isFloatingWindow = false;
#endregion

#region INITIALIZATION AND ONDISABLE

	/**
	 * Open the pb_Editor window with whatever dockable status is preference-d.
	 */
	public static pb_Editor MenuOpenWindow()
	{
		pb_Editor editor = (pb_Editor)EditorWindow.GetWindow(typeof(pb_Editor), !pb_Preferences_Internal.GetBool(pb_Constant.pbDefaultOpenInDockableWindow), pb_Constant.PRODUCT_NAME, true);			// open as floating window
		// would be nice if editorwindow's showMode was exposed
		editor.isFloatingWindow = !pb_Preferences_Internal.GetBool(pb_Constant.pbDefaultOpenInDockableWindow);
		return editor;
	}

	void OnEnable()
	{
		me = this;

		#if UNITY_4_3
			Undo.undoRedoPerformed += this.UndoRedoPerformed;
		#endif

		SharedProperties.PushToGridEvent += PushToGrid;

		HookSceneViewDelegate();

		// make sure load prefs is called first, because other methods depend on the preferences set here
		LoadPrefs();

		InitGUI();

		// checks for duplicate meshes created while probuilder was not open
		SceneWideDuplicateCheck();

		show_Detail 	= pb_Preferences_Internal.GetBool(pb_Constant.pbShowDetail);
		show_Mover 		= pb_Preferences_Internal.GetBool(pb_Constant.pbShowMover);
		show_Collider 	= pb_Preferences_Internal.GetBool(pb_Constant.pbShowCollider);
		show_Trigger 	= pb_Preferences_Internal.GetBool(pb_Constant.pbShowTrigger);
		show_NoDraw 	= pb_Preferences_Internal.GetBool(pb_Constant.pbShowNoDraw);

		// EditorUtility.UnloadUnusedAssets();
		ToggleEntityVisibility(EntityType.Detail, true);

		UpdateSelection(true);

		IntersectRayMesh = typeof(HandleUtility).GetMethod("IntersectRayMesh", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance);
		findNearestVertex = typeof(HandleUtility).GetMethod("FindNearestVertex", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance);

		// if( EditorPrefs.HasKey("pbShowUpgradeDialog") && EditorPrefs.GetBool("pbShowUpgradeDialog") )
		// {
		// 	EditorPrefs.SetBool("pbShowUpgradeDialog", false);	// see quickstart2

		// 	if(pb_Constant.pbVersion == "2.3.0")
		//	 	pb_Upgrade.Upgrade_2_3();
		// }
	}

	private Color selectedVertexColor = Color.green;
	private Color defaultVertexColor = Color.blue;
	public void InitGUI()
	{	
		selectedVertexColor = pb_Preferences_Internal.GetColor(pb_Constant.pbDefaultSelectedVertexColor);
		defaultVertexColor = pb_Preferences_Internal.GetColor(pb_Constant.pbDefaultVertexColor);
 		
 		// pbStyle = new GUIStyle();

		VertexTranslationInfoStyle = new GUIStyle();
		VertexTranslationInfoStyle.normal.background = EditorGUIUtility.whiteTexture;
		VertexTranslationInfoStyle.normal.textColor = new Color(1f, 1f, 1f, .6f);
		VertexTranslationInfoStyle.padding = new RectOffset(3,3,3,0);

		eye_on = (Texture2D)(Resources.Load(EditorGUIUtility.isProSkin ? "GUI/GenericIcons_16px_Eye_On" : "GUI/GenericIcons_16px_Eye_Off", typeof(Texture2D)));
		eye_off = (Texture2D)(Resources.Load(EditorGUIUtility.isProSkin ? "GUI/GenericIcons_16px_Eye_Off" : "GUI/GenericIcons_16px_Eye_On", typeof(Texture2D)));

		Texture2D face_Graphic_off = (Texture2D)(Resources.Load(EditorGUIUtility.isProSkin ? "GUI/ProBuilderGUI_Mode_Face-Off_Small-Pro" : "GUI/ProBuilderGUI_Mode_Face-Off_Small", typeof(Texture2D)));
		Texture2D vertex_Graphic_off = (Texture2D)(Resources.Load(EditorGUIUtility.isProSkin ? "GUI/ProBuilderGUI_Mode_Vertex-Off_Small-Pro" : "GUI/ProBuilderGUI_Mode_Vertex-Off_Small", typeof(Texture2D)));
		Texture2D edge_Graphic_off = (Texture2D)(Resources.Load(EditorGUIUtility.isProSkin ? "GUI/ProBuilderGUI_Mode_Edge-Off_Small-Pro" : "GUI/ProBuilderGUI_Mode_Edge-Off_Small", typeof(Texture2D)));

		SelectionIcons = new GUIContent[3]
		{
			new GUIContent(vertex_Graphic_off, "Vertex Selection"),
			new GUIContent(edge_Graphic_off, "Edge Selection"),
			new GUIContent(face_Graphic_off, "Face Selection")
		};

		show_NoDraw = true;		
		show_Detail = true;
		show_Mover = true;
		show_Collider = true;
		show_Trigger = true;

		this.minSize = new Vector2( isFloatingWindow ? WINDOW_WIDTH_FlOATING : WINDOW_WIDTH_DOCKABLE, 200 );
	}

	public void LoadPrefs()
	{
		// this exists to force update preferences when updating packages
		if(!EditorPrefs.HasKey(pb_Constant.pbEditorPrefVersion) || EditorPrefs.GetInt(pb_Constant.pbEditorPrefVersion) != EDITOR_PREF_VERSION ) {
			EditorPrefs.SetInt(pb_Constant.pbEditorPrefVersion, EDITOR_PREF_VERSION);
			EditorPrefs.DeleteKey(pb_Constant.pbDefaultShortcuts);
			Debug.LogWarning("ProBuilder: Clearing shortcuts. There were some internal changes in this release that required we rebuild this cache.  This will only happen once, and everything else is okay.");
		}

		editLevel 			= pb_Preferences_Internal.GetEnum<EditLevel>(pb_Constant.pbDefaultEditLevel);
		selectionMode		= pb_Preferences_Internal.GetEnum<SelectMode>(pb_Constant.pbDefaultSelectionMode);
		handleAlignment		= pb_Preferences_Internal.GetEnum<HandleAlignment>(pb_Constant.pbHandleAlignment);
				
		shortcuts 			= pb_Shortcut.ParseShortcuts(EditorPrefs.GetString(pb_Constant.pbDefaultShortcuts));
		limitFaceDragCheckToSelection = pb_Preferences_Internal.GetBool(pb_Constant.pbDragCheckLimit);
	}

	public void OnDisable()
	{
		ClearFaceSelection();
		UpdateSelection();
		if( OnSelectionUpdate != null )
			OnSelectionUpdate(null);

		SharedProperties.PushToGridEvent -= PushToGrid;

		SceneView.onSceneGUIDelegate -= this.OnSceneGUI;

		#if UNITY_4_3
			Undo.undoRedoPerformed -= this.UndoRedoPerformed;
		#endif

		EditorPrefs.SetInt(pb_Constant.pbHandleAlignment, (int)handleAlignment);

		pb_Editor_Graphics.OnDisable();
		// EditorUtility.UnloadUnusedAssetsIgnoreManagedReferences();	// this was fixed in 4.1.2
	}
#endregion

#region EVENT HANDLERS
	
	public delegate void OnSelectionUpdateEventHandler(pb_Object[] selection);
	public static event OnSelectionUpdateEventHandler OnSelectionUpdate;

	public delegate void OnVertexMovementFinishedEventHandler(pb_Object[] selection);
	public static event OnVertexMovementFinishedEventHandler OnVertexMovementFinished;

	public void HookSceneViewDelegate()
	{
		if(SceneView.onSceneGUIDelegate != this.OnSceneGUI)
		{
			SceneView.onSceneGUIDelegate -= this.OnSceneGUI;	// fuuuuck yooou lightmapping window
			SceneView.onSceneGUIDelegate += this.OnSceneGUI;
		}

		EditorApplication.playmodeStateChanged += OnPlayModeStateChanged;
	}
#endregion

#region ONGUI

	public void OnInspectorUpdate()
	{
		if(EditorWindow.focusedWindow != this)
			Repaint();
	}

	bool guiInitialized = false;

	/**
	 * Tool setting foldouts
	 */
	bool tool_growSelection = false;
	bool tool_extrudeButton = false;
	#if !PROTOTYPE
	bool tool_weldButton = false;
	#endif
	Vector2 scroll = Vector2.zero;
	Rect ToolbarRect_Select = new Rect(3,6,96,24);
	void OnGUI()
	{
		Event e = Event.current;	/// Different than OnSceneGUI currentEvent ?

		switch(e.type)
		{
			case EventType.ContextClick:
				OpenContextMenu();
				break;
		}

		if(!guiInitialized)
		{
			eye_style = new GUIStyle( EditorStyles.miniButtonRight );
			eye_style.padding = new RectOffset(0,0,0,0);
		}

		#region Static

			int t_selectionMode = editLevel != EditLevel.Top ? (int)selectionMode : -1;
			ToolbarRect_Select.x = (Screen.width/2 - 48) + (isFloatingWindow ? 1 : -1);

			EditorGUI.BeginChangeCheck();
			
			t_selectionMode = GUI.Toolbar(ToolbarRect_Select, (int)t_selectionMode, SelectionIcons, "Command");

			if(EditorGUI.EndChangeCheck())
			{
				if(t_selectionMode == (int)selectionMode && editLevel != EditLevel.Top)
				{
					SetEditLevel( EditLevel.Top );
				}
				else
				{
					if(editLevel == EditLevel.Top)
						SetEditLevel( EditLevel.Geometry );

					SetSelectionMode( (SelectMode)t_selectionMode );
					selectionMode = (SelectMode)t_selectionMode;
				}
			}

			GUILayout.Space( 34 );

			GUI.backgroundColor = pb_Constant.ProBuilderDarkGray;
			pb_GUI_Utility.DrawSeparator(2);
			GUI.backgroundColor = Color.white;

			if(editLevel == EditLevel.Geometry)
			{
				EditorGUI.BeginChangeCheck();
					handleAlignment = (HandleAlignment)EditorGUILayout.EnumPopup(new GUIContent("", "Toggle between Global, Local, and Plane Coordinates"), handleAlignment);
				if(EditorGUI.EndChangeCheck())
					SetHandleAlignment(handleAlignment);
				
				GUI.backgroundColor = pb_Constant.ProBuilderDarkGray;
				pb_GUI_Utility.DrawSeparator(1);
				GUI.backgroundColor = Color.white;
			}
		#endregion

		scroll = GUILayout.BeginScrollView(scroll);

		#region Tools

			if(GUILayout.Button(new GUIContent("Shape", "Open Shape Creation Panel"), EditorStyles.miniButton))
				OpenGeometryInterface();

			#if !PROTOTYPE
			if(GUILayout.Button(new GUIContent("Material", "Open Material Editor Window.  You can also Drag and Drop materials or textures to selected faces."), EditorStyles.miniButton))	
				pb_Material_Editor.MenuOpenMaterialEditor();
			#endif

			#if !PROTOTYPE
			if(GUILayout.Button(new GUIContent("UV Editor", "Open UV Editor Window"), EditorStyles.miniButton))	
				pb_UV_Editor.MenuOpenUVEditor();
			#endif

			if(GUILayout.Button(new GUIContent("Vertex Colors", "Provides an interface to set vertex colors.  Note that your shader must support vertex colors in order for changes to be visible."), EditorStyles.miniButton))	
				EditorWindow.GetWindow<pb_VertexColorInterface>(true, "Vertex Colors", true);

			#if !PROTOTYPE
			if(GUILayout.Button("Smoothing", EditorStyles.miniButton))
				pb_Smoothing_Editor.Init();
			#endif
		#endregion

		GUILayout.Space(2);
		GUI.backgroundColor = pb_Constant.ProBuilderDarkGray;
		pb_GUI_Utility.DrawSeparator(1);
		GUI.backgroundColor = Color.white;
		GUILayout.Space(2);

		#region Top
			if(editLevel == EditLevel.Top)
			{
				#if !PROTOTYPE

				GUI.enabled = true;
				if(GUILayout.Button(new GUIContent("Merge", "Combine all selected ProBuilder objects into a single object."), EditorStyles.miniButton))
					pb_Menu_Commands.MenuMergeObjects(selection);

				if(GUILayout.Button(new GUIContent("Mirror", "Open the Mirror Tool panel."), EditorStyles.miniButton)) 
					EditorWindow.GetWindow<pb_MirrorTool>(true, "Mirror Tool", true).Show();

				#endif
					
				if(GUILayout.Button(new GUIContent("Flip Normal", "If Top level, entire object normals are reversed.  Else only selected face normals are flipped."), EditorStyles.miniButton))
					pb_Menu_Commands.MenuFlipNormals(selection);

				#if !PROTOTYPE

				if(GUILayout.Button("Subdivide", EditorStyles.miniButton))
					pb_Menu_Commands.MenuSubdivide(selection);

				if(GUILayout.Button("Set Pivot", EditorStyles.miniButton))
					pb_Menu_Commands.MenuSetPivot(selection);

				GUILayout.Space(2);
				GUI.backgroundColor = pb_Constant.ProBuilderDarkGray;
				pb_GUI_Utility.DrawSeparator(1);
				GUI.backgroundColor = Color.white;
				GUILayout.Space(2);

				// Boolean operations

				// @todo Remove!
				// if(GUILayout.Button("Union"))
				// 	pb_Menu_Commands.MenuUnion(selection);

				// if(GUILayout.Button("Subtract"))
				// 	pb_Menu_Commands.MenuSubtract(selection);

				// if(GUILayout.Button("Intersect"))
				// 	pb_Menu_Commands.MenuIntersect(selection);

				#endif

				GUI.enabled = !EditorApplication.isPlaying;

				GUILayout.BeginHorizontal();
					if(GUILayout.Button("Set Detail", EditorStyles.miniButtonLeft))
					{
						pb_Menu_Commands.MenuSetEntityType(selection, EntityType.Detail);
						ToggleEntityVisibility(EntityType.Detail, show_Detail);
					}

					if(GUILayout.Button( show_Detail ? eye_on : eye_off, eye_style, GUILayout.MinWidth(28), GUILayout.MaxWidth(28), GUILayout.MaxHeight(15) ))
					{
						show_Detail = !show_Detail;
						EditorPrefs.SetBool(pb_Constant.pbShowDetail, show_Detail);
						ToggleEntityVisibility(EntityType.Detail, show_Detail);
					}
				GUILayout.EndHorizontal();

					GUILayout.BeginHorizontal();
					if(GUILayout.Button("Set Mover", EditorStyles.miniButtonLeft)) 
					{
						pb_Menu_Commands.MenuSetEntityType(selection, EntityType.Mover);
						ToggleEntityVisibility(EntityType.Mover, show_Mover);
					}

					if(GUILayout.Button( show_Mover ? eye_on : eye_off, eye_style, GUILayout.MinWidth(28), GUILayout.MaxWidth(28), GUILayout.MaxHeight(15) )) {
						show_Mover = !show_Mover;
						EditorPrefs.SetBool(pb_Constant.pbShowMover, show_Mover);
						ToggleEntityVisibility(EntityType.Mover, show_Mover);
					}
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
					if(GUILayout.Button("Set Collider", EditorStyles.miniButtonLeft)) 
					{
						pb_Menu_Commands.MenuSetEntityType(selection, EntityType.Collider);
						ToggleEntityVisibility(EntityType.Collider, show_Collider);
					}

					if(GUILayout.Button( show_Collider ? eye_on : eye_off, eye_style, GUILayout.MinWidth(28), GUILayout.MaxWidth(28), GUILayout.MaxHeight(15) )) {
						show_Collider = !show_Collider;
						EditorPrefs.SetBool(pb_Constant.pbShowCollider, show_Collider);
						ToggleEntityVisibility(EntityType.Collider, show_Collider);
					}
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
					if(GUILayout.Button("Set Trigger", EditorStyles.miniButtonLeft)) 
					{
						pb_Menu_Commands.MenuSetEntityType(selection, EntityType.Trigger);
						ToggleEntityVisibility(EntityType.Trigger, show_Trigger);
					}

					if(GUILayout.Button( show_Trigger ? eye_on : eye_off, eye_style, GUILayout.MinWidth(28), GUILayout.MaxWidth(28), GUILayout.MaxHeight(15) )) {
						show_Trigger = !show_Trigger;
						EditorPrefs.SetBool(pb_Constant.pbShowTrigger, show_Trigger);
						ToggleEntityVisibility(EntityType.Trigger, show_Trigger);
					}
				GUILayout.EndHorizontal();

				GUILayout.Space(3);
				GUI.backgroundColor = pb_Constant.ProBuilderDarkGray;
				pb_GUI_Utility.DrawSeparator(1);
				GUI.backgroundColor = Color.white;
				GUILayout.Space(3);

				#if !PROTOTYPE
				GUILayout.BeginHorizontal();

					GUILayout.Label("NoDraw", EditorStyles.miniButtonLeft); 

					if(GUILayout.Button( show_NoDraw ? eye_on : eye_off, eye_style, GUILayout.MinWidth(28), GUILayout.MaxWidth(28), GUILayout.MaxHeight(15) )) {
						show_NoDraw = !show_NoDraw;
						EditorPrefs.SetBool(pb_Constant.pbShowNoDraw, show_NoDraw);
						ToggleNoDrawVisibility(show_NoDraw);
					}
				GUILayout.EndHorizontal();

				#endif

				GUI.enabled = true;
			}
		#endregion

		#region Geometry

			// Soft Select
			// Soft Selection Distance Value
			// Soft Selection Falloff Value
			// Use Angle
			// Angle Value
			GUI.enabled = true;
			
			if(editLevel != EditLevel.Top)
			{
				/********************************************************
				*						Selection 						*
				********************************************************/
				GUI.enabled = selectedVertexCount > 0;
				
				tool_growSelection = ToolSettingsGUI("Grow", "Adds adjacent faces to the current selection.  Optionally can restrict augmentation to faces within a restricted angle difference.",
					tool_growSelection,
					pb_Menu_Commands.MenuGrowSelection,
					pb_Menu_Commands.GrowSelectionGUI,
					selectionMode == SelectMode.Face,
					40);

				if(GUILayout.Button("Shrink", EditorStyles.miniButton))
					pb_Menu_Commands.MenuShrinkSelection(selection);

				if(GUILayout.Button("Invert", EditorStyles.miniButton))
					pb_Menu_Commands.MenuInvertSelection(selection);

				/********************************************************
				*						Edge Level 						*
				********************************************************/
				if(selectionMode == SelectMode.Edge)
				{
					GUI.enabled = selectedEdgeCount > 0;
					if(GUILayout.Button("Loop", EditorStyles.miniButton)) 
						pb_Menu_Commands.MenuLoopSelection(selection);

					if(GUILayout.Button("Ring", EditorStyles.miniButton))
						pb_Menu_Commands.MenuRingSelection(selection);
				}

				/********************************************************
				*						Face Level 						*
				********************************************************/
				if(editLevel == EditLevel.Geometry)
				{
					GUI.backgroundColor = pb_Constant.ProBuilderDarkGray;
					pb_GUI_Utility.DrawSeparator(1);
					GUI.backgroundColor = Color.white;

					/********************************************************
					*					Always Show 						*
					********************************************************/

					GUI.enabled = selectedVertexCount > 0;

					if(GUILayout.Button(new GUIContent("Set Pivot", "Set the pivot of selected geometry to the center of the current element selection."), EditorStyles.miniButton))
						pb_Menu_Commands.MenuSetPivot(selection);

					GUI.enabled = selectedFaceCount > 0 || selectedEdgeCount > 0;
					
					tool_extrudeButton = ToolSettingsGUI("Extrude", "Extrude the currently selected elements by a set amount.  Also try holding 'Shift' while moving the handle tool.",
						tool_extrudeButton,
						pb_Menu_Commands.MenuExtrude,
						pb_Menu_Commands.ExtrudeButtonGUI,
						20);

					GUI.enabled = selectedFaceCount > 0;

					#if !PROTOTYPE

					if(GUILayout.Button("Flip Normals", EditorStyles.miniButton))
						pb_Menu_Commands.MenuFlipNormals(selection);

					if(GUILayout.Button("Delete", EditorStyles.miniButton)) 
						pb_Menu_Commands.MenuDeleteFace(selection);

					if(GUILayout.Button("Detach", EditorStyles.miniButton))
						pb_Menu_Commands.MenuDetachFacesContext(selection);

					switch(selectionMode)
					{
						case SelectMode.Face:

							if(GUILayout.Button("Subdiv Face", EditorStyles.miniButton))
								pb_Menu_Commands.MenuSubdivideFace(selection);
							break;

						case SelectMode.Edge:

							GUI.enabled = selectedEdgeCount == 2;
							if(GUILayout.Button("Bridge", EditorStyles.miniButton))
								pb_Menu_Commands.MenuBridgeEdges(selection);

							GUI.enabled = selectedEdgeCount > 1;
							if(GUILayout.Button("Connect", EditorStyles.miniButton)) 
								pb_Menu_Commands.MenuConnectEdges(selection);

							GUI.enabled = selectedEdgeCount > 0;
							if(GUILayout.Button(new GUIContent("Insert Loop", "Inserts an Edge loop by selecting the edge ring, then connecting the centers of all edges."), EditorStyles.miniButton))
								pb_Menu_Commands.MenuInsertEdgeLoop(selection);

							break;

						case SelectMode.Vertex:

							GUI.enabled = per_object_vertexCount_distinct > 1;
							if(GUILayout.Button("Connect", EditorStyles.miniButton))
								pb_Menu_Commands.MenuConnectVertices(selection);

							tool_weldButton = ToolSettingsGUI("Weld", "Merge selected vertices that are within a specified distance of one another.",
								tool_weldButton,
								pb_Menu_Commands.MenuWeldVertices,
								pb_Menu_Commands.WeldButtonGUI,
								20);

							if(GUILayout.Button("Collapse", EditorStyles.miniButton))
								pb_Menu_Commands.MenuCollapseVertices(selection);

							GUI.enabled = per_object_vertexCount_distinct > 0;
							if(GUILayout.Button("Split", EditorStyles.miniButton))
								pb_Menu_Commands.MenuSplitVertices(selection);
							
							break;
					}

					#endif
				}
			}

			GUI.enabled = true;

			#if PB_DEBUG && BUGGER
			
			GUILayout.Space(4);
			GUI.backgroundColor = pb_Constant.ProBuilderDarkGray;
			pb_GUI_Utility.DrawSeparator(2);
			GUI.backgroundColor = Color.white;
			GUILayout.Space(4);

			if(GUILayout.Button("times",GUILayout.MinWidth(20)))
			{
				Bugger.Log(profiler.ToString());
			}

			if(GUILayout.Button("reset",GUILayout.MinWidth(20)))
			{
				profiler.Reset();
			}
			#endif
		#endregion
		
		GUILayout.EndScrollView();
	}

	bool ToolSettingsGUI(string text, string description, bool showSettings, System.Action<pb_Object[]> action, System.Action gui, int guiHeight)
	{
		return ToolSettingsGUI(text, description, showSettings, action, gui, true, guiHeight);
	}

	bool ToolSettingsGUI(string text, string description, bool showSettings, System.Action<pb_Object[]> action, System.Action gui, bool enabled, int guiHeight)
	{
		if(enabled)
		{
			GUILayout.BeginHorizontal();
			if(GUILayout.Button(new GUIContent(text, description), EditorStyles.miniButtonLeft))
				action(selection);

			if(GUILayout.Button( showSettings ? "-" : "+", EditorStyles.miniButtonRight, GUILayout.MaxWidth(24)))
				showSettings = !showSettings;
			GUILayout.EndHorizontal();

			if(showSettings)
			{
				GUI.backgroundColor = TOOL_SETTINGS_COLOR;
				Rect al = GUILayoutUtility.GetLastRect();
				GUI.Box( new Rect(al.x, al.y + al.height + 2, al.width, guiHeight), "");
				GUI.backgroundColor = Color.white;

				gui();
				GUILayout.Space(4);
			}
		}
		else
		{
			if(GUILayout.Button(new GUIContent(text, description), EditorStyles.miniButton))
				action(selection);
		}

		return showSettings;
	}
#endregion

#region CONTEXT MENU
	
	void OpenContextMenu()
	{
		GenericMenu menu = new GenericMenu();

		menu.AddItem (new GUIContent("Open As Floating Window", ""), false, Menu_OpenAsFloatingWindow);
		menu.AddItem (new GUIContent("Open As Dockable Window", ""), false, Menu_OpenAsDockableWindow);

		// menu.AddSeparator("");
		menu.ShowAsContext ();
	}		

	void Menu_OpenAsDockableWindow()
	{
		EditorPrefs.SetBool(pb_Constant.pbDefaultOpenInDockableWindow, true);

		EditorWindow.GetWindow<pb_Editor>().Close();
		pb_Editor.MenuOpenWindow();
	}

	void Menu_OpenAsFloatingWindow()
	{
		EditorPrefs.SetBool(pb_Constant.pbDefaultOpenInDockableWindow, false);

		EditorWindow.GetWindow<pb_Editor>().Close();
		pb_Editor.MenuOpenWindow();
	}
#endregion

#region ONSCENEGUI

	// GUI Caches
	public pb_Object[] selection = new pb_Object[0];							// All selected pb_Objects
	
	public int selectedVertexCount { get; private set; }					// Sum of all vertices sleected
	public int selectedFaceCount { get; private set; }						// Sum of all faces sleected
	public int selectedEdgeCount { get; private set; }						// Sum of all edges sleected

	// the mouse vertex selection box
	private Rect mouseRect = new Rect(0,0,0,0);

	// Handles
	Tool currentHandle = Tool.Move;

	// Dragging
	Vector2 mousePosition_initial;
	Rect selectionRect;
	Color dragRectColor = new Color(.313f, .8f, 1f, 1f);
	private bool dragging = false;
	private bool doubleClicked = false;	// prevents leftClickUp from stealing focus after double click

	// vertex handles
	Vector3 newPosition, cachedPosition;
	bool movingVertices = false;

	// top level caching
	bool scaling = false;

	private bool rightMouseDown = false;
	Event currentEvent;// = (Event)0;

	void OnSceneGUI(SceneView scnView)
	{	
		currentEvent = Event.current;
		
		if(currentEvent.Equals(Event.KeyboardEvent("v")))
		{
			currentEvent.Use();
			snapToVertex = true;
		}

		/**
		 * Snap stuff
		 */
		if(currentEvent.type == EventType.KeyUp)
			snapToVertex = false;

		if(currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
			rightMouseDown = true;

		if(currentEvent.type == EventType.MouseUp && currentEvent.button == 1 || currentEvent.type == EventType.Ignore)
			rightMouseDown = false;

		#if !UNITY_4_3
		if(currentEvent.type == EventType.ValidateCommand)
		{
			OnValidateCommand(currentEvent.commandName);
			return;
		}
		#endif

		#if !PROTOTYPE
			// e.type == EventType.DragUpdated || 
			if(currentEvent.type == EventType.DragPerform)
			{
				GameObject go = HandleUtility.PickGameObject(currentEvent.mousePosition, false);

				if(go != null && System.Array.Exists(DragAndDrop.objectReferences, x => x is Texture2D || x is Material))
				{
					pb_Object pb = go.GetComponent<pb_Object>();

					if( pb )
					{
						Material mat = null;
						foreach(Object t in DragAndDrop.objectReferences)
						{
							if(t is Material)
							{
								mat = (Material)t;
								break;
							}
							/* This works, but throws some bullshit errors. Not creating a material leaks, so disable this functionality. */
							else
							if(t is Texture2D)
							{
								mat = new Material(Shader.Find("Diffuse"));
								mat.mainTexture = (Texture2D)t;

								string texPath = AssetDatabase.GetAssetPath(mat.mainTexture);
								int lastDot = texPath.LastIndexOf(".");
								texPath = texPath.Substring(0, texPath.Length - (texPath.Length-lastDot));
								texPath = AssetDatabase.GenerateUniqueAssetPath(texPath + ".mat");

								AssetDatabase.CreateAsset(mat, texPath);
								AssetDatabase.Refresh();

								break;
							}
						}

						if(mat != null)
						{
							if(editLevel == EditLevel.Geometry)
							{
								pbUndo.RecordObjects(selection, "Set Face Materials");

								foreach(pb_Object pbs in selection)
									pbs.SetFaceMaterial(pbs.SelectedFaces.Length < 1 ? pbs.faces : pbs.SelectedFaces, mat);

							}
							else
							{
								pbUndo.RecordObject(pb, "Set Object Material");
								pb.SetFaceMaterial(pb.faces, mat);
							}

							pb.GenerateUV2(true);
							pb.Refresh();

							currentEvent.Use();
						}
					}
				}
			}
		#endif

		DrawHandleGUI();

		if(!rightMouseDown && getKeyUp != KeyCode.None)
		{
			if(ShortcutCheck())
			{
				currentEvent.Use();
				return;
			}
		}

		// Listen for top level movement	
		ListenForTopLevelMovement();
		
		// Finished moving vertices, scaling, or adjusting uvs
		#if PROTOTYPE
		if( (movingVertices || scaling) && GUIUtility.hotControl < 1)
		{
			OnFinishedVertexModification();
		}
		#else
		if( (movingVertices || movingPictures || scaling) && GUIUtility.hotControl < 1)
		{
			OnFinishedVertexModification();
			UpdateTextureHandles();
		}
		#endif

		// Check mouse position in scene and determine if we should highlight something
		UpdateMouse(currentEvent.mousePosition);

		// Draw GUI Handles
		if(editLevel != EditLevel.Top)
			DrawHandles();

		DrawVertexNormals(drawVertexNormals);

		if(drawFaceNormals) DrawFaceNormals();

		if(Tools.current != Tool.None && Tools.current != currentHandle)
			SetTool_Internal(Tools.current);

		if( (editLevel == EditLevel.Geometry || editLevel == EditLevel.Texture) && Tools.current != Tool.View)
		{
			if( selectedVertexCount > 0 ) 
			{
				if(editLevel == EditLevel.Geometry)
				{
					switch(currentHandle)
					{
						case Tool.Move:
							VertexMoveTool();
							break;
						case Tool.Scale:
							VertexScaleTool();
							break;
						case Tool.Rotate:
							VertexRotateTool();
							break;
					}
				}
				#if !PROTOTYPE	// TEXTURE HANDLES
				else if(editLevel == EditLevel.Texture && selectedVertexCount > 0)
				{
					switch(currentHandle)
					{
						case Tool.Move:
							TextureMoveTool();
							break;
						case Tool.Rotate:
							TextureRotateTool();
							break;
						case Tool.Scale:
							TextureScaleTool();
							break;
					}
		 		}
		 		#endif
		 	}
		}
		else
		{
			return;
		}

		// altClick || Tools.current == Tool.View || GUIUtility.hotControl > 0 || middleClick
		// Tools.viewTool == ViewTool.FPS || Tools.viewTool == ViewTool.Orbit
		if( pb_Handle_Utility.SceneViewInUse(currentEvent) || currentEvent.isKey)
		{
			dragging = false;

			return;
		}

		/* * * * * * * * * * * * * * * * * * * * *
		 *	 Vertex & Quad Wranglin' Ahead! 	 *
		 * 	 Everything from this point below	 *
		 *	 overrides something Unity related.  *
		 * * * * * * * * * * * * * * * * * * * * */

		// This prevents us from selecting other objects in the scene,
		// and allows for the selection of faces / vertices.
		int controlID = GUIUtility.GetControlID(FocusType.Passive);
		HandleUtility.AddDefaultControl(controlID);

		// If selection is made, don't use default handle -- set it to Tools.None
		if(selectedVertexCount > 0)
			Tools.current = Tool.None;

		if(leftClick) {
			// double clicking object
			if(currentEvent.clickCount > 1)
			{
				DoubleClick(currentEvent);
			}

			mousePosition_initial = mousePosition;
		}

		if(mouseDrag)
			dragging = true;

		if(ignore)
		{
			if(dragging)
			{
				dragging = false;
				DragCheck();
			}
			
			if(doubleClicked)
				doubleClicked = false;
		}

		if(leftClickUp)
		{
			if(doubleClicked)
			{
				doubleClicked = false;
			}
			else
			{
				if(!dragging)
				{
					#if !PROTOTYPE
					if(pb_UV_Editor.instance)
						pb_UV_Editor.instance.ResetUserPivot();
					#endif

					RaycastCheck(currentEvent.mousePosition);
				}
				else
				{
					dragging = false;

					#if !PROTOTYPE
					if(pb_UV_Editor.instance)
						pb_UV_Editor.instance.ResetUserPivot();
					#endif

					DragCheck();
				}
			}
		}

		if(GUI.changed) {
			foreach(pb_Object pb in selection)
				EditorUtility.SetDirty(pb);
		}
	}

	void DoubleClick(Event e)
	{
		pb_Object pb = RaycastCheck(e.mousePosition);
		if(pb != null)
		{
			if(selectionMode == SelectMode.Edge)
			{
				if(e.shift)
					pb_Menu_Commands.MenuRingSelection(selection);
				else
					pb_Menu_Commands.MenuLoopSelection(selection);
			}
			else
				pb.SetSelectedFaces(pb.faces);

			UpdateSelection(false);
			SceneView.RepaintAll();
			doubleClicked = true;
		}
	}
#endregion

#region RAYCASTING AND DRAGGING

	public const float MAX_EDGE_SELECT_DISTANCE = 7;
	int nearestEdgeObjectIndex = -1;
	int nearestEdgeIndex = -1;
	pb_Edge nearestEdge;	

	/**
	 * If in Edge mode, finds the nearest Edge to the mouse
	 */
	private void UpdateMouse(Vector3 mousePosition)
	{
		if(selection.Length < 1) return;

		switch(selectionMode)
		{
			// default:
			case SelectMode.Edge:

				// TODO
				float bestDistance = MAX_EDGE_SELECT_DISTANCE;				
				int bestEdgeIndex = -1;
				int objIndex = -1;
				try 
				{
					for(int i = 0; i < selected_universal_edges_all.Length; i++)
					{
						pb_Edge[] edges = selected_universal_edges_all[i];
						
						for(int j = 0; j < edges.Length; j++)
						{
							int x = selection[i].sharedIndices[edges[j].x][0];
							int y = selection[i].sharedIndices[edges[j].y][0];

							float d = HandleUtility.DistanceToLine(selected_verticesInWorldSpace_all[i][x], selected_verticesInWorldSpace_all[i][y]);
							
							if(d < bestDistance)
							{
								objIndex = i;
								bestEdgeIndex = j;
								bestDistance = d;
							}
						}
					}
				} catch( System.Exception e) {
				}
				
				if(bestEdgeIndex != nearestEdgeIndex || objIndex != nearestEdgeObjectIndex)
				{
					nearestEdgeObjectIndex = objIndex;
					nearestEdgeIndex = bestEdgeIndex;

					if(nearestEdgeIndex > -1)
						nearestEdge = new pb_Edge(
							selection[objIndex].sharedIndices[selected_universal_edges_all[objIndex][nearestEdgeIndex].x][0],
							selection[objIndex].sharedIndices[selected_universal_edges_all[objIndex][nearestEdgeIndex].y][0]);

					SceneView.RepaintAll();
				}
				break;
		}
	}

	// Returns the pb_Object modified by this action.  If no action taken, or action is eaten by texture window, return null.
	// A pb_Object is returned because double click actions need to know what the last selected pb_Object was.
	private pb_Object RaycastCheck(Vector3 mousePosition)
	{
		pb_Object pb;
		if(selectionMode == SelectMode.Edge)
		{
			if(EdgeClickCheck(out pb))
			{
				UpdateSelection(false);
				SceneView.RepaintAll();
				return pb;
			}
		}

		GameObject nearestGameObject = HandleUtility.PickGameObject(mousePosition, false);

		if(nearestGameObject)
		{
			// if we clicked a pb_Object
			pb = nearestGameObject.GetComponent<pb_Object>();
			if(pb && pb.isSelectable)
			{
				// check for shift key.  if not, change selection to clicked object
				if(!shiftKey && !ctrlKey)
				{
					// assuming that it is not the currently selected
					if(nearestGameObject != Selection.activeGameObject)
					{
						SetSelection(nearestGameObject);

						// Uncomment to require object selection before editing
						// 	return pb;
					}
					else
					{
						// otherwise, set the selection to just the nearest gameObject
						// and continue
						SetSelection(nearestGameObject);
					}
				}
				else
				// if shift key is held, and we haven't already selected this object, add it to the selection
				if(shiftKey || ctrlKey)
				{
					// if adding, don't allow for face selection.  if it's already selected, move on to face selection
					if(!Selection.Contains(nearestGameObject))
					{
						AddToSelection(nearestGameObject);
					}
				}
			}
			else 
			{
			  // Check prefrences to see if only Probuilder objects are selectable
			  if (pb_Preferences_Internal.GetBool(pb_Constant.pbPBOSelectionOnly))
			  {
				  if (nearestGameObject.GetComponent<pb_Object>())
				  {
					  Exit(nearestGameObject);
				  }
			  }
			  else
			  {
				  Exit(nearestGameObject);
			  }

			  return null;
			}
		}
		// Clicked off gameObject.  Return selection to native functions.
		else
		{
			if(selectionMode == SelectMode.Vertex)
			{
				if(!VertexClickCheck(out pb))
				{
					Exit();
					return null;
				}
				else
				{
					UpdateSelection(false);
					SceneView.RepaintAll();
					return pb;
				}
			}
			else if(selectionMode == SelectMode.Edge)
			{
				if(!EdgeClickCheck(out pb))
				{
					Exit();
					return null;
				}
				else
				{
					UpdateSelection(false);
					SceneView.RepaintAll();
					return pb;
				}
			}
			else
			{
				Exit();
				return null;
			}
		}
		
		Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
		RaycastHit hit;

		// if( Physics.Raycast(ray.origin, ray.direction, out hit))
		object[] parameters = new object[] { ray, pb.msh, pb.transform.localToWorldMatrix, null };
		if(IntersectRayMesh == null) IntersectRayMesh = typeof(HandleUtility).GetMethod("IntersectRayMesh", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance);
		object result = IntersectRayMesh.Invoke(this, parameters);	

		if ( (bool)result )
			hit = (RaycastHit)parameters[3];
		else
			return pb;

		pb_Object vpb;
		/**
		 *	If in vertex selection mode, check for a vertex click before checking for a face.
		 */
		if(selectionMode == SelectMode.Vertex && VertexClickCheck(out vpb))
		{
			pb = vpb;
		}
		// Then check if edge mode, and test for edgeVertices
		else
		if(selectionMode == SelectMode.Edge && EdgeClickCheck(out vpb))
		{
			pb = vpb;
		}
		// finally, fall back on face selection
		else
		{
			// Check for face hit
			Mesh m = pb.msh;

			int[] tri = new int[3] {
				m.triangles[hit.triangleIndex * 3 + 0], 
				m.triangles[hit.triangleIndex * 3 + 1], 
				m.triangles[hit.triangleIndex * 3 + 2] 
			};
			
			pb_Face selectedFace;
			int selectedFaceIndex;
			if( pb.FaceWithTriangle(tri, out selectedFaceIndex) )
			{
				selectedFace = pb.faces[selectedFaceIndex];

				/**
				 * Check for other editor mouse shortcuts first - todo: better way to do this.
				 */

				#if !PROTOTYPE
				pb_Material_Editor matEditor = pb_Material_Editor.instance;
				if( matEditor != null && matEditor.ClickShortcutCheck(Event.current.modifiers, pb, selectedFace) )
					return pb;

				pb_UV_Editor uvEditor = pb_UV_Editor.instance;
				if(uvEditor != null && uvEditor.ClickShortcutCheck(pb, selectedFace))
					return pb;
				#endif


				// Check to see if we've already selected this quad.  If so, remove it from selection cache.
				pbUndo.RecordObject(pb, "Change Face Selection");

				int indx = System.Array.IndexOf(pb.SelectedFaces, selectedFace);
				if( indx > -1 ) {
					pb.RemoveFromFaceSelectionAtIndex(indx);
				} else {
					pb.AddToFaceSelection(selectedFaceIndex);
				}
			}
		}

		Event.current.Use();

		UpdateSelection(false);
		SceneView.RepaintAll();

		return pb;
	}

	private bool VertexClickCheck(out pb_Object vpb)
	{
		if(!shiftKey && !ctrlKey) ClearFaceSelection();
		

		for(int i = 0; i < selection.Length; i++)
		{
			pb_Object pb = selection[i];
			if(!pb.isSelectable) continue;

			for(int n = 0; n < selected_uniqueIndices_all[i].Length; n++)
			{
				Vector3 v = selected_verticesInWorldSpace_all[i][selected_uniqueIndices_all[i][n]];

				if(mouseRect.Contains(HandleUtility.WorldToGUIPoint(v)))
				{
					// Check if index is already selected, and if not add it to the pot
					int indx = System.Array.IndexOf(pb.SelectedTriangles, selected_uniqueIndices_all[i][n]);

					// @all vertex handles
					if(!Selection.Contains(pb.gameObject))
						AddToSelection(pb.gameObject);

					pbUndo.RecordObject(pb, "Change Vertex Selection");

					// If we get a match, check to see if it exists in our selection array already, then add / remove
					if( indx > -1 )
						pb.SetSelectedTriangles(pb.SelectedTriangles.RemoveAt(indx));
					else
						pb.SetSelectedTriangles(pb.SelectedTriangles.Add(selected_uniqueIndices_all[i][n]));

					vpb = pb;
					return true;
				}
			}
		}

		vpb = null;
		return false;
	}

	private bool EdgeClickCheck(out pb_Object pb)
	{
		#if PB_DEBUG
		profiler.BeginSample("EdgeClickCheck");
		#endif

		if(nearestEdgeObjectIndex > -1)
		{
			pb = selection[nearestEdgeObjectIndex];

			if(!shiftKey && !ctrlKey) pb.ClearSelection();

			if(nearestEdgeIndex > -1)
			{
				pb_Edge edge;
				
				if( pb_Edge.ValidateEdge(pb, nearestEdge, out edge) )
					nearestEdge = edge;

				int ind = pb.SelectedEdges.IndexOf(nearestEdge, pb.sharedIndices);
				
				pbUndo.RecordObject(pb, "Change Edge Selection");
				
				if( ind > -1 )
					pb.SetSelectedEdges(pb.SelectedEdges.RemoveAt(ind));
				else
					pb.SetSelectedEdges(pb.SelectedEdges.Add(nearestEdge));
			}

			#if PB_DEBUG
			profiler.EndSample();
			#endif
			return true;
		}
		else
		{
			if(!shiftKey && !ctrlKey)
				ClearFaceSelection();

			pb = null;

			#if PB_DEBUG
			profiler.EndSample();
			#endif

			return false;
		}
	}

	private void DragCheck()
	{
		Camera cam = SceneView.lastActiveSceneView.camera;
		limitFaceDragCheckToSelection = pb_Preferences_Internal.GetBool(pb_Constant.pbDragCheckLimit);

		pbUndo.RecordObjects(selection, "Drag Select");

		switch(selectionMode)
		{
			case SelectMode.Vertex:
			{

				if(!shiftKey && !ctrlKey) ClearFaceSelection();
				
				for(int i = 0; i < selection.Length; i++)
				{
					pb_Object pb = selection[i];
					if(!pb.isSelectable) continue;

					List<int> selectedTriangles = new List<int>(pb.SelectedTriangles);

					for(int n = 0; n < selected_uniqueIndices_all[i].Length; n++)
					{
						Vector3 v = selected_verticesInWorldSpace_all[i][selected_uniqueIndices_all[i][n]];

						if(selectionRect.Contains(HandleUtility.WorldToGUIPoint(v)))
						{
							// if point is behind the camera, ignore it.
							if(cam.WorldToScreenPoint(v).z < 0)
								continue;
							
							// Check if index is already selected, and if not add it to the pot
							int indx = selectedTriangles.IndexOf(selected_uniqueIndices_all[i][n]);

							// @todo condense this to a single array rebuild
							if(indx > -1)
								selectedTriangles.RemoveAt(indx);
							else
								selectedTriangles.Add(selected_uniqueIndices_all[i][n]);
						}
					}
					
					pb.SetSelectedTriangles(selectedTriangles.ToArray());
				}

				if(!vertexSelectionMask)
					DragObjectCheck(true);
                
                UpdateSelection(false);
			}
			break;

			case SelectMode.Face:
			{
				if(!shiftKey && !ctrlKey) ClearFaceSelection();

				pb_Object[] pool = limitFaceDragCheckToSelection ? selection : (pb_Object[])FindObjectsOfType(typeof(pb_Object));

				List<pb_Face> selectedFaces;
				for(int i = 0; i < pool.Length; i++)
				{
					pb_Object pb = pool[i];
					selectedFaces = new List<pb_Face>(pb.SelectedFaces);

					if(!pb.isSelectable) continue;

					Vector3[] verticesInWorldSpace = limitFaceDragCheckToSelection ? selected_verticesInWorldSpace_all[i] : pb.VerticesInWorldSpace();
					bool addToSelection = false;
					for(int n = 0; n < pb.faces.Length; n++)
					{
						pb_Face face = pb.faces[n];
						// only check the first index per quad, and if it checks out, then check every other point
						if(selectionRect.Contains(HandleUtility.WorldToGUIPoint(verticesInWorldSpace[face.indices[0]])))
						{
							bool nope = false;
							for(int q = 0; q < pb.faces[n].distinctIndices.Length; q++)
							{
								if(!selectionRect.Contains(HandleUtility.WorldToGUIPoint(verticesInWorldSpace[face.distinctIndices[q]])))
								{
									nope = true;
									break;
								}
							}

							if(!nope)
							{
								int indx =  selectedFaces.IndexOf(face);
								
								if( indx > -1 ) {
									selectedFaces.RemoveAt(indx);
								} else {
									addToSelection = true;
									selectedFaces.Add(face);
								}
							}
						}
					}

					pb.SetSelectedFaces(selectedFaces.ToArray());
					if(addToSelection)
						AddToSelection(pb.gameObject);
				}

				DragObjectCheck(true);

				UpdateSelection(false);
			}
			break;

			case SelectMode.Edge:
			{
				if(!shiftKey && !ctrlKey) ClearFaceSelection();

				#if PB_DEBUG
				profiler.BeginSample("Drag Select Edges");
				#endif

				for(int e = 0; e < selection.Length; e++)
				{
					pb_Object pb = selection[e];
					List<pb_Edge> inSelection = new List<pb_Edge>();

					for(int i = 0; i < selected_universal_edges_all[e].Length; i++)
					{
						// pb_Edge edge = new pb_Edge(
						// 	pb.sharedIndices[selected_universal_edges_all[e][i].x][0],
						// 	pb.sharedIndices[selected_universal_edges_all[e][i].y][0]);

						pb_Edge edge = pb_Edge.GetLocalEdge(pb, selected_universal_edges_all[e][i]);

						if( selectionRect.Contains(HandleUtility.WorldToGUIPoint(selected_verticesInWorldSpace_all[e][edge.x])) &&
							selectionRect.Contains(HandleUtility.WorldToGUIPoint(selected_verticesInWorldSpace_all[e][edge.y])))
							inSelection.Add(edge);
					}

					List<pb_Edge> add = new List<pb_Edge>();
					List<int> remove = new List<int>();
					foreach(pb_Edge edge in inSelection.Distinct())
					{
						int ind = pb.SelectedEdges.IndexOf(edge, pb.sharedIndices);
						// int ind = System.Array.IndexOf(pb.SelectedEdges, edge);
						
						if(ind > -1)
							remove.Add(ind);
						else
							add.Add(edge);
					}

					List<pb_Edge> priorSelection = new List<pb_Edge>(pb.SelectedEdges.RemoveAt(remove.ToArray()));
					priorSelection.AddRange(add);
					pb.SetSelectedEdges(priorSelection.ToArray());
				}

				if(!vertexSelectionMask)
					DragObjectCheck(true);
				
				#if PB_DEBUG
				profiler.EndSample();
				#endif

				UpdateSelection(false);
			}
			break;

		default:
			DragObjectCheck(false);
			break;
		}

		SceneView.RepaintAll();
	}

	// Emulates the usual Unity drag to select objects functionality
	private void DragObjectCheck(bool vertexMode)
	{
		// if we're in vertex selection mode, only add to selection if shift key is held, 
		// and don't clear the selection if shift isn't held.
		// if not, behave regularly (clear selection if shift isn't held)
		if(!vertexMode) {
			if(!shiftKey) ClearSelection();
		} else {
			if(!shiftKey && selectedVertexCount > 0) return;
		}

		// scan for new selected objects
		/// if mode based, don't allow selection of non-probuilder objects
		if(!limitFaceDragCheckToSelection)
		{
			foreach(pb_Object g in HandleUtility.PickRectObjects(selectionRect).GetComponents<pb_Object>())
				if(!Selection.Contains(g.gameObject))
					AddToSelection(g.gameObject);
		}
	}
#endregion

#region VERTEX TOOLS

	private bool snapToVertex = false;
	private Vector3 previousHandleScale = Vector3.one;
	private Vector3 currentHandleScale = Vector3.one;
	private Vector3[][] vertexOrigins;
	private Vector3[] vertexOffset;
	private Quaternion previousHandleRotation = Quaternion.identity;
	private Quaternion currentHandleRotation = Quaternion.identity;
	
	private Vector3 translateOrigin = Vector3.zero;
	private Vector3 rotateOrigin = Vector3.zero;
	private Vector3 scaleOrigin = Vector3.zero;

	private void VertexMoveTool()
	{
		newPosition = selected_handlePivotWorld;
		cachedPosition = newPosition;

		#if !UNITY_4_3
		Undo.ClearSnapshotTarget();
		#endif

		newPosition = Handles.PositionHandle(newPosition, handleRotation);

		if(altClick) return;

		bool previouslyMoving = movingVertices;
		if(newPosition != cachedPosition)
		{
			Vector3 diff = newPosition-cachedPosition;

			Vector3 mask = new Vector3(
				Mathf.Abs(diff.x) > .0001f ? 1f : 0f,
				Mathf.Abs(diff.y) > .0001f ? 1f : 0f,
				Mathf.Abs(diff.z) > .0001f ? 1f : 0f);

			if(snapToVertex)
			{
				Vector3 v;
				if( FindNearestVertex(mousePosition, out v) )
					diff = Vector3.Scale(v-cachedPosition, mask);
			}

			movingVertices = true;
			if(previouslyMoving == false)
			{
				translateOrigin = cachedPosition;
				rotateOrigin = currentHandleRotation.eulerAngles;
				scaleOrigin = currentHandleScale;

				#if !UNITY_4_3
				Undo.SetSnapshotTarget(pbUtil.GetComponents<pb_Object>(Selection.transforms) as Object[], "Move Vertices");
				Undo.CreateSnapshot();
				Undo.RegisterSnapshot();
				#endif

				if(Event.current.modifiers == EventModifiers.Shift)
					ShiftExtrude();
			}

			#if UNITY_4_3
				Undo.RecordObjects(pbUtil.GetComponents<pb_Object>(Selection.transforms) as Object[], "Move Vertices");
			#endif

			for(int i = 0; i < selection.Length; i++)
			{
				selection[i].TranslateVertices_World(selection[i].SelectedTriangles, diff);

				selection[i].RefreshUV( SelectedFacesInEditZone[i] );
				selection[i].RefreshNormals();
			}

			Internal_UpdateSelectionFast();
		}
	}

	private void VertexScaleTool()
	{
		newPosition = selected_handlePivotWorld;

		previousHandleScale = currentHandleScale;

		#if !UNITY_4_3
		Undo.ClearSnapshotTarget();
		#endif
	
		currentHandleScale = Handles.ScaleHandle(currentHandleScale, newPosition, handleRotation, HandleUtility.GetHandleSize(newPosition));

		if(altClick) return;

		bool previouslyMoving = movingVertices;
	
		if(previousHandleScale != currentHandleScale)
		{
			movingVertices = true;
			if(previouslyMoving == false)
			{
				translateOrigin = cachedPosition;
				rotateOrigin = currentHandleRotation.eulerAngles;
				scaleOrigin = currentHandleScale;

				#if !UNITY_4_3
				Undo.SetSnapshotTarget(pbUtil.GetComponents<pb_Object>(Selection.transforms) as Object[], "Scale Vertices");
				Undo.CreateSnapshot();
				Undo.RegisterSnapshot();
				#endif

				if(Event.current.modifiers == EventModifiers.Shift)
					ShiftExtrude();

				// cache vertex positions for scaling later
				vertexOrigins = new Vector3[selection.Length][];
				vertexOffset = new Vector3[selection.Length];

				for(int i = 0; i < selection.Length; i++)
				{	
					vertexOrigins[i] = selection[i].GetVertices(selection[i].SelectedTriangles);
					vertexOffset[i] = pb_Math.Average(vertexOrigins[i]);
				}
			}

			Vector3 ver;	// resulting vertex from modification
			Vector3 over;	// vertex point to modify. different for world, local, and plane
			
			#if UNITY_4_3
				Undo.RecordObjects(pbUtil.GetComponents<pb_Object>(Selection.transforms) as Object[], "Scale Vertices");
			#endif

			bool gotoWorld = Selection.transforms.Length > 1 && handleAlignment == HandleAlignment.Plane;
			bool gotoLocal = selectedFaceCount < 1;

			for(int i = 0; i < selection.Length; i++)
			{
				for(int n = 0; n < selection[i].SelectedTriangles.Length; n++)
				{
					switch(handleAlignment)
					{
						case HandleAlignment.Plane:
							if(gotoWorld)
								goto case HandleAlignment.World;

							if(gotoLocal)
								goto case HandleAlignment.Local;

							Quaternion localRot = Quaternion.identity;

							// get the plane rotation in local space
							Vector3 nrm = pb_Math.Normal(vertexOrigins[i]);

							localRot = Quaternion.LookRotation(nrm == Vector3.zero ? Vector3.forward : nrm, Vector3.up);	
	
							// move center of vertices to 0,0,0 and set rotation as close to identity as possible					
							over = Quaternion.Inverse(localRot) * (vertexOrigins[i][n] - vertexOffset[i]);

							// apply scale
							ver = Vector3.Scale(over, currentHandleScale);
							// re-apply original rotation
							if(vertexOrigins[i].Length > 2)
								ver = localRot * ver;
							// re-apply world position offset
							ver += vertexOffset[i];
							// set the vertex in local space
							selection[i].SetSharedVertexPosition(selection[i].SelectedTriangles[n], ver);
							
							break;

						case HandleAlignment.World:
						case HandleAlignment.Local:
							// move vertex to relative origin from center of selection
							over = vertexOrigins[i][n] - vertexOffset[i];
							// apply scale
							ver = Vector3.Scale(over, currentHandleScale);
							// move vertex back to locally offset position
							ver += vertexOffset[i];
							// set vertex in local space on pb-Object
							selection[i].SetSharedVertexPosition(selection[i].SelectedTriangles[n], ver);
							break;
					}
				}
			
				selection[i].RefreshUV( SelectedFacesInEditZone[i] );
				selection[i].RefreshNormals();
			}

			Internal_UpdateSelectionFast();
		}
	}

	private void VertexRotateTool()
	{
		newPosition = selected_handlePivotWorld;

		previousHandleRotation = currentHandleRotation;

		#if !UNITY_4_3
		Undo.ClearSnapshotTarget();
		#endif

		if(altClick)
			Handles.RotationHandle(currentHandleRotation, newPosition);
		else
			currentHandleRotation = Handles.RotationHandle(currentHandleRotation, newPosition);

		bool previouslyMoving = movingVertices;

		if(currentHandleRotation != previousHandleRotation)
		{
			movingVertices = true;
			if(previouslyMoving == false)
			{
				translateOrigin = cachedPosition;
				rotateOrigin = currentHandleRotation.eulerAngles;
				scaleOrigin = currentHandleScale;
				
				#if !UNITY_4_3
				Undo.SetSnapshotTarget(pbUtil.GetComponents<pb_Object>(Selection.transforms) as Object[], "Rotate Vertices");
				Undo.CreateSnapshot();
				Undo.RegisterSnapshot();
				#endif

				if(Event.current.modifiers == EventModifiers.Shift)
					ShiftExtrude();

				// cache vertex positions for modifying later
				vertexOrigins = new Vector3[selection.Length][];
				vertexOffset = new Vector3[selection.Length];

				for(int i = 0; i < selection.Length; i++)
				{					
					vertexOrigins[i] = selection[i].GetVertices(selection[i].SelectedTriangles).ToArray();
					vertexOffset[i] = pb_Math.Average(vertexOrigins[i]);
				}
			}
			
			#if UNITY_4_3
				Undo.RecordObjects(pbUtil.GetComponents<pb_Object>(Selection.transforms) as Object[], "Scale Vertices");
			#endif

			Vector3 ver;	// resulting vertex from modification
			for(int i = 0; i < selection.Length; i++)
			{
				Quaternion transformedRotation;
				switch(handleAlignment)
				{
					case HandleAlignment.Plane:
						int facesLength = selection[i].SelectedFaceIndices.Length;
						if(facesLength < 1) goto case HandleAlignment.Local;	// can't do plane without a plane

						Quaternion inverseLocalRotation = Quaternion.Inverse(selection[i].transform.localRotation);
						Vector3 nrm = facesLength > 0 ? pb_Math.Normal(vertexOrigins[i]) : Vector3.zero;
						Quaternion inversePlaneRotation = Quaternion.Inverse( nrm == Vector3.zero ? Quaternion.identity : Quaternion.LookRotation(nrm, Vector3.up) );
			
						transformedRotation = inverseLocalRotation * currentHandleRotation * inversePlaneRotation;
						break;

					case HandleAlignment.Local:
						transformedRotation = Quaternion.Inverse(selection[i].transform.localRotation) * currentHandleRotation;
						break;

					default:
						transformedRotation = currentHandleRotation;
						break;
				}

				if(selection.Length > 1)	// use world when selection is > 1 objects
				{
					for(int n = 0; n < selection[i].SelectedTriangles.Length; n++)
					{
						// vertex offset relative to object
						ver = vertexOrigins[i][n] - vertexOffset[i];

						// move to world space
						ver = selection[i].transform.localToWorldMatrix * ver;

						// apply handle rotation
						ver = currentHandleRotation * ver;

						// move back to local space
						ver = selection[i].transform.worldToLocalMatrix * ver;

						// and back to vertex position
						ver += vertexOffset[i];

						// now set that mofo in the msh.vertices array
						selection[i].SetSharedVertexPosition(selection[i].SelectedTriangles[n], ver);
					}
				}
				else
				{
					for(int n = 0; n < selection[i].SelectedTriangles.Length; n++)
					{
						// move vertex to relative origin from center of selection
						ver = vertexOrigins[i][n] - vertexOffset[i];

						ver = transformedRotation * ver;

						// move vertex back to locally offset position
						ver += vertexOffset[i];

						selection[i].SetSharedVertexPosition(selection[i].SelectedTriangles[n], ver);
					}
				}

				// set vertex in local space on pb-Object

				selection[i].RefreshUV( SelectedFacesInEditZone[i] );
				selection[i].RefreshNormals();
			}

			// don't modify the handle rotation because otherwise rotating with plane coordinates
			// updates the handle rotation with every change, making moving things a changing target
			Quaternion rotateToolHandleRotation = currentHandleRotation;
			
			Internal_UpdateSelectionFast();
			
			currentHandleRotation = rotateToolHandleRotation;
		}
	}

	private void ShiftExtrude()
	{
		pbUndo.RecordObjects(pbUtil.GetComponents<pb_Object>(Selection.transforms) as Object[], "Shift+Extrude Faces");

		int ef = 0;
		foreach(pb_Object pb in selection)
		{
			switch(selectionMode)
			{
				case SelectMode.Edge:

					if(pb.SelectedFaceCount > 0)
						goto default;

					pb_Edge[] newEdges;
					bool success = pb.Extrude(pb.SelectedEdges, 0f, pb_Preferences_Internal.GetBool(pb_Constant.pbManifoldEdgeExtrusion), out newEdges);

					if(success)
					{
						ef += newEdges.Length;
						pb.SetSelectedEdges(newEdges);
					}
					break;

				default:
					int len = pb.SelectedFaces.Length;

					if(len > 0)
					{
						pb.Extrude(pb.SelectedFaces, 0f);
						pb.SetSelectedFaces(pb.SelectedFaces);

						ef += len;
					}
					break;
			}
		}

		if(ef > 0)
		{
			pb_Editor_Utility.ShowNotification("Extrude");
			UpdateSelection(true);
		}
	}

	private bool FindNearestVertex(Vector2 mousePosition, out Vector3 vertex)
	{
		List<Transform> t = new List<Transform>((Transform[])pbUtil.GetComponents<Transform>(HandleUtility.PickRectObjects(new Rect(0,0,Screen.width,Screen.height))));
		
		GameObject nearest = HandleUtility.PickGameObject(mousePosition, false);
		if(nearest != null)
			t.Add(nearest.transform);

		object[] parameters = new object[] { (Vector2)mousePosition, t.ToArray(), null };
		if(findNearestVertex == null) findNearestVertex = typeof(HandleUtility).GetMethod("findNearestVertex", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance);

		object result = findNearestVertex.Invoke(this, parameters);	
		vertex = (bool)result ? (Vector3)parameters[2] : Vector3.zero;
		return (bool)result;
	}

	#if !PROTOTYPE
	Vector3 textureHandle = Vector3.zero;
	Vector3 handleOrigin = Vector3.zero;
	bool movingPictures = false;

	private void TextureMoveTool()
	{
		pb_UV_Editor uvEditor = pb_UV_Editor.instance;
		if(!uvEditor) return;

		Vector3 cached = textureHandle;

		textureHandle = Handles.PositionHandle(textureHandle, handleRotation);

		if(altClick) return;
		
		if(textureHandle != cached)
		{
			if(!movingPictures)
			{
				handleOrigin = cached;
				movingPictures = true;
			}

			cached = Quaternion.Inverse(handleRotation) * textureHandle;
			cached.y = -cached.y;

			Vector3 lossyScale = selection[0].transform.lossyScale;
			uvEditor.SceneMoveTool( cached.DivideBy(lossyScale), handleOrigin.DivideBy(lossyScale) );
			uvEditor.Repaint();
		}
	}

	Quaternion textureRotation = Quaternion.identity;
	private void TextureRotateTool()
	{
		pb_UV_Editor uvEditor = pb_UV_Editor.instance;
		if(!uvEditor) return;

		float size = HandleUtility.GetHandleSize(selected_handlePivotWorld);

		if(altClick) return;

		Matrix4x4 prev = Handles.matrix;
		Handles.matrix = handleMatrix;

		Quaternion cached = textureRotation;

		textureRotation = Handles.Disc(textureRotation, Vector3.zero, Vector3.forward, size, false, 0f);

		if(textureRotation != cached)
		{
			if(!movingPictures)
				movingPictures = true;

			uvEditor.SceneRotateTool(-textureRotation.eulerAngles.z);
		}

		Handles.matrix = prev;
	}

	Vector3 textureScale = Vector3.one;

	private void TextureScaleTool()
	{
		pb_UV_Editor uvEditor = pb_UV_Editor.instance;
		if(!uvEditor) return;

		float size = HandleUtility.GetHandleSize(selected_handlePivotWorld);

		Matrix4x4 prev = Handles.matrix;
		Handles.matrix = handleMatrix;

		Vector3 cached = textureScale;
		textureScale = Handles.ScaleHandle(textureScale, Vector3.zero, Quaternion.identity, size);

		if(altClick) return;

		if(cached != textureScale)
		{
			if(!movingPictures)
				movingPictures = true;

			uvEditor.SceneScaleTool(textureScale, cached);
		}

		Handles.matrix = prev;
	}
	#endif
#endregion

#region HANDLE DRAWING

	public void DrawHandles ()
	{
		Handles.lighting = false;

		switch(selectionMode)
		{

			case SelectMode.Face:
				pb_Editor_Graphics.DrawSelectionMesh();
				break;

			case SelectMode.Vertex:
			{		
				if(selection.Length > 0)
				{
					pb_Editor_Graphics.DrawVertexHandles(selection.Length, selected_uniqueIndices_all, selected_verticesInWorldSpace_all, defaultVertexColor);
					pb_Editor_Graphics.DrawVertexHandles(selection.Length, selected_uniqueIndices_sel, selected_verticesInWorldSpace_all, selectedVertexColor);
				}
					// pb_Editor_Graphics.DrawSelectionMesh();
			}
			break;
	
		#if !UNITY_4			
			case SelectMode.Edge:

				Handles.color = Color.blue;

				// TODO - figure out how to run UpdateSelection prior to an Undo event.
				// Currently UndoRedoPerformed is called after the action has taken place.
				try
				{
					for(int i = 0; i < selected_universal_edges_all.Length; i++)
					{
						pb_Object pb = selection[i];
						for(int e = 0; e < selected_universal_edges_all[i].Length; e++)
						{
							pb_Edge edge = selected_universal_edges_all[i][e];
							
							if(edge.x >= pb.sharedIndices.Length || edge.y >= pb.sharedIndices.Length)
								break;

							Handles.DrawLine(selected_verticesInWorldSpace_all[i][pb.sharedIndices[edge.x][0]], selected_verticesInWorldSpace_all[i][pb.sharedIndices[edge.y][0]]);
						}
					}
					Handles.color = Color.green;

					for(int i = 0; i < selection.Length; i++)
					{
						for(int j = 0; j < selection[i].SelectedEdges.Length; j++)
						{
							pb_Object pb = selection[i];
							Vector3[] v = selected_verticesInWorldSpace_all[i];
		
							Handles.DrawLine(v[pb.SelectedEdges[j].x], v[pb.SelectedEdges[j].y]);
						}
					}
				} catch (System.Exception e) {}

				if(nearestEdgeObjectIndex > -1 && nearestEdgeIndex > -1)
				{
					Handles.color = Color.red;
					Handles.DrawLine(
						selected_verticesInWorldSpace_all[nearestEdgeObjectIndex][nearestEdge.x],
						selected_verticesInWorldSpace_all[nearestEdgeObjectIndex][nearestEdge.y]);
				}
				Handles.color = Color.white;
				
				break;
		#endif
		}

		Handles.lighting = true;
	}

	Color handleBgColor;
	public void DrawHandleGUI()
	{
		Handles.BeginGUI();

		handleBgColor = GUI.backgroundColor;

		#if SVN_EXISTS
		// SVN
		GUI.Label(new Rect(4, 4, 200, 40), "r" + revisionNo);
		#endif

		if(movingVertices)
		{
			GUI.backgroundColor = pb_Constant.ProBuilderLightGray;
			// Handles.Label(newPosition,
			// 	"Translate: " + (newPosition-translateOrigin).ToString() + 
			// 	"\nRotate: " + (currentHandleRotation.eulerAngles-rotateOrigin).ToString() +
			// 	"\nScale: " + (currentHandleScale-scaleOrigin).ToString()
			// 	, VertexTranslationInfoStyle);
			GUI.Label(new Rect(Screen.width-200, Screen.height-120, 162, 48), 
				"Translate: " + (newPosition-translateOrigin).ToString() + 
				"\nRotate: " + (currentHandleRotation.eulerAngles-rotateOrigin).ToString() +
				"\nScale: " + (currentHandleScale-scaleOrigin).ToString()
				, VertexTranslationInfoStyle
				);
		}

		#if DEBUG
		int startY = 0;
		GUI.Label(new Rect(18, startY += 20, 200, 40), "Faces: " + faceCount);
		GUI.Label(new Rect(18, startY += 20, 200, 40), "Vertices: " + vertexCount + " : " + (selection != null && selection.Length > 0 ? selection[0].msh.vertexCount : 0).ToString());
		GUI.Label(new Rect(18, startY += 20, 200, 40), "UVs: " + (selection != null && selection.Length > 0 ? (selection[0].uv.Length.ToString() + " : " + selection[0].msh.uv.Length.ToString()) : "0") );
		GUI.Label(new Rect(18, startY += 20, 200, 40), "Triangles: " + triangleCount);
		startY += 20;
		GUI.Label(new Rect(18, startY += 20, 200, 40), "Selected Faces: " + selectedFaceCount);
		GUI.Label(new Rect(18, startY += 20, 200, 40), "Selected Vertices: " + selectedVertexCount);
		#endif

		// Enables vertex selection with a mouse click
		if(editLevel == EditLevel.Geometry && !dragging && selectionMode == SelectMode.Vertex)
			mouseRect = new Rect(Event.current.mousePosition.x-10, Event.current.mousePosition.y-10, 20, 20);
		else
			mouseRect = pb_Constant.RectZero;

		// Draw selection rect if dragging

		if(dragging)
		{
			GUI.backgroundColor = dragRectColor;
			// Always draw from lowest to largest values
			Vector2 start = Vector2.Min(mousePosition_initial, mousePosition);
			Vector2 end = Vector2.Max(mousePosition_initial, mousePosition);

			selectionRect = new Rect(start.x, start.y, 
				end.x - start.x, end.y - start.y);

			GUI.Box(selectionRect, "");			

			HandleUtility.Repaint();
		}

		GUI.backgroundColor = handleBgColor;

		Handles.EndGUI();
	}
#endregion

#region SHORTCUT

	private bool ShortcutCheck()
	{
		int shortcut = pb_Shortcut.IndexOf(shortcuts, Event.current.keyCode, Event.current.modifiers);

		if( shortcut < 0 )
			return false;

		bool used = true;

		// #if PROTOTYPE
		// if(shortcuts[shortcut].action == "Texture Mode")
		// 	return false;
		// #endif

		used = AllLevelShortcuts(shortcuts[shortcut]);		

		if(!used)
		switch(editLevel)
		{
			case EditLevel.Top:
				used = TopLevelShortcuts(shortcuts[shortcut]);
				break;

			case EditLevel.Texture:
				goto case EditLevel.Geometry;

			case EditLevel.Geometry:
				used = GeoLevelShortcuts(shortcuts[shortcut]);
				break;

			default:
				used = false;
				break;
		}

		if(used)
		{
			if(	shortcuts[shortcut].action != "Delete Face" &&
				shortcuts[shortcut].action != "Escape" &&
				shortcuts[shortcut].action != "Quick Apply Nodraw" &&
				shortcuts[shortcut].action != "Toggle Geometry Mode" &&
				shortcuts[shortcut].action != "Toggle Handle Pivot" &&
				shortcuts[shortcut].action != "Toggle Selection Mode" )
				pb_Editor_Utility.ShowNotification(shortcuts[shortcut].action);
	
			Event.current.Use();
		}

		shortcut = -1;

		return used;
	}

	private bool AllLevelShortcuts(pb_Shortcut shortcut)
	{
		bool used = true;
		switch(shortcut.action)
		{
			// TODO Remove once a workaround for non-upper-case shortcut chars is found
			case "Toggle Geometry Mode":

				if(editLevel == EditLevel.Geometry)
				{
					pb_Editor_Utility.ShowNotification("Top Level Editing");
					SetEditLevel(EditLevel.Top);
				}
				else
				{
					pb_Editor_Utility.ShowNotification("Geometry Editing");
					SetEditLevel(EditLevel.Geometry);
				}
				break;

			case "Vertex Mode":
				if(editLevel == EditLevel.Top)	
					SetEditLevel(EditLevel.Geometry);
				SetSelectionMode( SelectMode.Vertex );
				break;

			case "Edge Mode":
				if(editLevel == EditLevel.Top)	
					SetEditLevel(EditLevel.Geometry);
				SetSelectionMode( SelectMode.Edge );
				break;

			case "Face Mode":
				if(editLevel == EditLevel.Top)	
					SetEditLevel(EditLevel.Geometry);
				SetSelectionMode( SelectMode.Face );
				break;

			default:
				used = false;
				break;
		}

		return used;
	}

	private bool TopLevelShortcuts(pb_Shortcut shortcut)
	{
		if(selection == null || selection.Length < 1 || editLevel != EditLevel.Top)
			return false;

		bool used = true;

		switch(shortcut.action)
		{
			/* ENTITY TYPES */
			case "Set Trigger":
					pb_Menu_Commands.MenuSetEntityType(selection, EntityType.Trigger);
				break;

			#if !PROTOTYPE
			case "Set Occluder":
					pb_Menu_Commands.MenuSetEntityType(selection, EntityType.Occluder);
				break;
			#endif

			case "Set Collider":
					pb_Menu_Commands.MenuSetEntityType(selection, EntityType.Collider);
				break;

			case "Set Mover":
					pb_Menu_Commands.MenuSetEntityType(selection, EntityType.Mover);
				break;
				
			case "Set Detail":
					pb_Menu_Commands.MenuSetEntityType(selection, EntityType.Detail);
				break;

			default:	
				used = false;
				break;
		}

		return used;
	}

	private bool GeoLevelShortcuts(pb_Shortcut shortcut)
	{
		bool used = true;
		switch(shortcut.action)
		{
			case "Escape":
				ClearFaceSelection();
				pb_Editor_Utility.ShowNotification("Top Level");
				UpdateSelection(false);
				SetEditLevel(EditLevel.Top);
			break;
		
			// TODO Remove once a workaround for non-upper-case shortcut chars is found			
			case "Toggle Selection Mode":
				ToggleSelectionMode();
				switch(selectionMode)
				{
					case SelectMode.Face:
						pb_Editor_Utility.ShowNotification("Editing Faces");
						break;

					case SelectMode.Vertex:
						pb_Editor_Utility.ShowNotification("Editing Vertices");
						break;

					case SelectMode.Edge:
						pb_Editor_Utility.ShowNotification("Editing Edges");
						break;
				}
				break;

			#if !PROTOTYPE
			case "Quick Apply Nodraw":
		
				if(editLevel != EditLevel.Top)
					pb_Editor_Utility.ShowNotification(shortcut.action);

				pb_Material_Editor.ApplyMaterial(selection, pb_Constant.NoDrawMaterial);
				ClearFaceSelection();
				break;
			#endif

			#if !PROTOTYPE
			case "Delete Face":
				pbUndo.RecordObjects(selection, "Delete selected faces.");

				int sel_faces = 0;
				foreach(pb_Object pb in selection)
				{
					sel_faces += pb.SelectedFaces.Length;

					pb.DeleteFaces(pb.SelectedFaces);

					if(pb.faces.Length < 1)
					{
						pbUndo.DestroyImmediate(pb, "Delete Object");
					}
					else
					{
						pb.GenerateUV2(show_NoDraw);
						pb.Refresh();
					}
				}

				if(sel_faces > 0)
					pb_Editor_Utility.ShowNotification(shortcut.action);

				ClearFaceSelection();

				UpdateSelection(true);

				OnGeometryChanged(selection);

				break;
			#endif

			/* handle alignment */
			// TODO Remove once a workaround for non-upper-case shortcut chars is found
			case "Toggle Handle Pivot":
				if(selectedVertexCount < 1)
					break;

				if(editLevel != EditLevel.Texture)
				{		
					ToggleHandleAlignment();
					pb_Editor_Utility.ShowNotification("Handle Alignment: " + ((HandleAlignment)handleAlignment).ToString());
				}
				break;

			case "Set Pivot":

		        if (selection.Length > 0)
		        {
					foreach (pb_Object pbo in selection)
					{
						pbUndo.RecordObjects(new Object[2] {pbo, pbo.transform}, "Set Pivot");

						if (pbo.SelectedTriangles.Length > 0)
						{
							pbo.CenterPivot(pbo.SelectedTriangles);
						}
						else
						{
							pbo.CenterPivot(null);
						}
					}
				}
				break;

			default:
				used = false;
				break;
		}
		return used;
	}
#endregion

#region VIS GROUPS

	public static bool show_NoDraw;
	bool show_Detail;
	bool show_Mover;
	bool show_Collider;
	bool show_Trigger;

	public void ToggleEntityVisibility(EntityType entityType, bool isVisible)
	{
		foreach(pb_Entity sel in Object.FindObjectsOfType(typeof(pb_Entity)))
		{
			if(sel.entityType == entityType) {
				sel.GetComponent<MeshRenderer>().enabled = isVisible;
				if(sel.GetComponent<MeshCollider>())
					sel.GetComponent<MeshCollider>().enabled = isVisible;
			}
		}		
	}
	
	public void ToggleNoDrawVisibility(bool show)
	{
		#if !PROTOTYPE
		foreach(pb_Object pb in GameObject.FindObjectsOfType(typeof(pb_Object)))
			pb.ToMesh(!show_NoDraw);// param is hideNoDraw
		#endif
	}
#endregion

#region TOOL SETTINGS

	/**
	 * Allows another window to tell the Editor what Tool is now in use.
	 * Does *not* update any other windows.
	 */
	public void SetTool(Tool newTool)
	{
		currentHandle = newTool;
	}	

	/**
	 * Calls SetTool(), then Updates the UV Editor window if applicable.
	 */
	private void SetTool_Internal(Tool newTool)
	{
		SetTool(newTool);

		#if !PROTOTYPE
		if(pb_UV_Editor.instance != null)
			pb_UV_Editor.instance.SetTool(newTool);
		#endif
	}

	public void SetHandleAlignment(HandleAlignment ha)
	{
		if(editLevel == EditLevel.Texture)
			ha = HandleAlignment.Plane;
		else
			EditorPrefs.SetInt(pb_Constant.pbHandleAlignment, (int)ha);
		
		handleAlignment = ha;

		UpdateHandleRotation();

		currentHandleRotation = handleRotation;

		SceneView.RepaintAll();

		// todo
		Repaint();
	}

	public void ToggleHandleAlignment()
	{
		int newHa = (int)handleAlignment+1;
		if( newHa >= System.Enum.GetValues(typeof(HandleAlignment)).Length)
			newHa = 0;
		SetHandleAlignment((HandleAlignment)newHa);
	}

	public void ToggleEditLevel()
	{
		if(editLevel == EditLevel.Geometry)
			SetEditLevel(EditLevel.Top);
		else
			SetEditLevel(EditLevel.Geometry);
	}

	/**
	 * Toggles between the SelectMode values and updates the graphic handles
	 * as necessary.
	 */
	public void ToggleSelectionMode()
	{
		int smode = (int)selectionMode;
		smode++;
		if(smode >= SELECT_MODE_LENGTH)
			smode = 0;
		SetSelectionMode( (SelectMode)smode );
	}

	/**
	 * \brief Sets the current selection mode @SelectMode to the mode value.
	 */
	public void SetSelectionMode(SelectMode mode)
	{
		selectionMode = mode;

		#if UNITY_4
			pb_Editor_Graphics.UpdateSelectionMesh(selection, selectionMode);
		#else
		if(selectionMode == SelectMode.Face)
		{
			pb_Editor_Graphics.UpdateSelectionMesh(selection, selectionMode);
			Internal_UpdateSelectionFast();
		}
		else
		{
			pb_Editor_Graphics.OnDisable();
			UpdateSelection(false);
		}
		#endif

		SceneView.RepaintAll();

		EditorPrefs.SetInt(pb_Constant.pbDefaultSelectionMode, (int)selectionMode);
	}

	/**
	 * Set the EditLevel back to its last level.
	 */
	public void PopEditLevel()
	{
		SetEditLevel(previousEditLevel);
	}

	/**
	 * Changes the current Editor level - switches between Object, Sub-object, and Texture (hidden).
	 */
	public void SetEditLevel(EditLevel el)
	{	
		previousEditLevel = editLevel;

		switch(el)
		{
			case EditLevel.Top:
				ClearFaceSelection();		
				UpdateSelection(true);

				SetSelection(Selection.gameObjects);
				break;

			case EditLevel.Geometry:
				
				Tools.current = Tool.None;

				UpdateSelection(false);
				SceneView.RepaintAll();
				break;

			#if !PROTOTYPE
			case EditLevel.Texture:
				
				previousHandleAlignment = handleAlignment;
				previousSelectMode = selectionMode;

				SetHandleAlignment(HandleAlignment.Plane);
				break;
			#endif
		}

		editLevel = el;

		#if !PROTOTYPE
		if(previousEditLevel == EditLevel.Texture && el != EditLevel.Texture)
		{
			SetSelectionMode(previousSelectMode);
			SetHandleAlignment(previousHandleAlignment);
		}
		#endif

		if(editLevel != EditLevel.Texture)
			EditorPrefs.SetInt(pb_Constant.pbDefaultEditLevel, (int)editLevel);
	}
#endregion

#region SELECTION CACHING AND MANAGING
	
	/** 
	 *	\brief Updates the arrays used to draw GUI elements (both Window and Scene).
	 *	@selection_vertex should already be populated at this point.  UpdateSelection 
	 *	just removes duplicate indices, and populates the gui arrays for displaying
	 *	 things like quad faces and vertex billboards.
	 */

	int[][] 			selected_uniqueIndices_all = new int[0][];
	int[][] 			selected_uniqueIndices_sel = new int[0][];
	Vector3[][] 		selected_verticesInWorldSpace_all = new Vector3[0][];
	Vector3[][] 		selected_verticesLocal_sel = new Vector3[0][];
	
	pb_Edge[][] 		selected_universal_edges_all = new pb_Edge[0][];

	public pb_Edge[][]  Selected_Universal_Edges_All { get { return selected_universal_edges_all; } }
	public pb_Face[][] 	SelectedFacesInEditZone { get; private set; }//new pb_Face[0][];		// faces that need to be refreshed when moving or modifying the actual selection
	public Vector3[][]  SelectedVerticesInWorldSpace { get { return selected_verticesInWorldSpace_all; } }

	public Vector3		HandlePositionWorld { get { return selected_handlePivotWorld; } }
	Vector3				selected_handlePivotWorld = Vector3.zero;
	// Vector3[]			selected_handlePivot = new Vector3[0];
	int 				per_object_vertexCount_distinct = 0;	///< The number of selected distinct indices on the object with the greatest number of selected distinct indices.

	#if DEBUG
	int faceCount = 0;
	int vertexCount = 0;
	int triangleCount = 0;
	#endif

	public void UpdateSelection() { UpdateSelection(true); }
	public void UpdateSelection(bool forceUpdate)
	{	
		#if PB_DEBUG
		profiler.BeginSample("UpdateSelection");
		#endif
		
		per_object_vertexCount_distinct = 0;
		
		selectedVertexCount = 0;
		selectedFaceCount = 0;
		selectedEdgeCount = 0;

		#if DEBUG
		faceCount = 0;
		vertexCount = 0;
		triangleCount = 0;
		#endif

		pb_Object[] t_selection = selection;

		#if PB_DEBUG
		profiler.BeginSample("GetComponents");
			selection = pbUtil.GetComponents<pb_Object>(Selection.transforms);
		profiler.EndSample();
		profiler.BeginSample("Heavy Update Stuff");
		#else
		selection = pbUtil.GetComponents<pb_Object>(Selection.transforms);
		#endif
		
		// If the top level selection has changed, update all the heavy cache things
		// that don't change based on element selction
		if(forceUpdate || !t_selection.SequenceEqual(selection))
		{
			forceUpdate = true;	// If updating due to inequal selections, set the forceUpdate to true so some of the functions below know that these values
								// can be trusted.
			selected_universal_edges_all 		= new pb_Edge[selection.Length][];
			selected_verticesInWorldSpace_all 	= new Vector3[selection.Length][];
			selected_uniqueIndices_all			= new int[selection.Length][];

			for(int i = 0; i < selection.Length; i++)
			{
				#if PB_DEBUG
				profiler.BeginSample("selected_uniqueIndices_all");
				#endif
				selected_uniqueIndices_all[i] = selection[i].msh.triangles.Distinct().ToArray();//pb.uniqueIndices;

				// necessary only once on selection modification
				#if PB_DEBUG
				profiler.EndSample();
				profiler.BeginSample("pb_Edge.GetUniversalEdges");
				#endif

				selected_universal_edges_all[i] = pb_Edge.GetUniversalEdges(pb_Edge.AllEdges(selection[i].faces), selection[i].sharedIndices).Distinct().ToArray();
				
				#if PB_DEBUG
				profiler.EndSample();
				profiler.BeginSample("VerticesInWorldSpace_all");
				#endif

				selected_verticesInWorldSpace_all[i] = selection[i].VerticesInWorldSpace();	// to speed this up, could just get uniqueIndices vertiecs

				#if PB_DEBUG
				profiler.EndSample();
				#endif
			}
		}

		transformCache = new Vector3[selection.Length][];

		selected_uniqueIndices_sel			= new int[selection.Length][];
		selected_verticesLocal_sel			= new Vector3[selection.Length][];
		SelectedFacesInEditZone 			= new pb_Face[selection.Length][];
		// selected_handlePivot 				= new Vector3[selection.Length];
		
		selected_handlePivotWorld			= Vector3.zero;

		#if PB_DEBUG
		profiler.EndSample();
		#endif
		
		Vector3 min = Vector3.zero, max = Vector3.zero;
		bool boundsInitialized = false;

		for(int i = 0; i < selection.Length; i++)
		{			
			pb_Object pb = selection[i];

			if(!boundsInitialized && pb.SelectedTriangleCount > 0)	
			{
				boundsInitialized = true;
				min = pb.transform.TransformPoint(pb.vertices[pb.SelectedTriangles[0]]);
				max = min;
			}

			#if PB_DEBUG
			profiler.BeginSample("Cache Transform");
			#endif 

			transformCache[i] = new Vector3[3]
			{
				pb.transform.position,
				pb.transform.localRotation.eulerAngles,
				pb.transform.localScale
			};

			#if PB_DEBUG
			profiler.EndSample();
			profiler.BeginSample("unique::SelectedTriangles");
			#endif

			// things necessary to call every frame
			selected_uniqueIndices_sel[i] = pb.SelectedTriangles;			

			
			#if PB_DEBUG
			profiler.EndSample();
			profiler.BeginSample("selected_verticesLocal_sel");
			#endif

			selected_verticesLocal_sel[i] = pb.GetVertices(pb.SelectedTriangles);

			#if PB_DEBUG
			profiler.EndSample();
			// profiler.BeginSample("selected_handlePivot");
			#endif

			// selected_handlePivot[i] = pb_Math.Average(selected_verticesLocal_sel[i]);
			
			#if PB_DEBUG
			// profiler.EndSample();
			profiler.BeginSample("selected_handlePivotWorld");
			#endif

			if(pb.SelectedTriangles.Length > 0)
			{
				if(forceUpdate)
				{
					foreach(Vector3 v in pbUtil.ValuesWithIndices(selected_verticesInWorldSpace_all[i], pb.SelectedTriangles))
					{
						min = Vector3.Min(min, v);
						max = Vector3.Max(max, v);
					}
				}
				else
				{
					foreach(Vector3 v in pb.VerticesInWorldSpace(pb.SelectedTriangles))
					{
						min = Vector3.Min(min, v);
						max = Vector3.Max(max, v);
					}
				}
			}
			
			#if PB_DEBUG
			profiler.EndSample();
			profiler.BeginSample("SelectedFacesInEditZone");
			#endif

			SelectedFacesInEditZone[i] = pbMeshUtils.GetNeighborFaces(pb, pb.SelectedTriangles);
			
			#if PB_DEBUG
			profiler.EndSample();
			#endif

			selectedVertexCount += selection[i].SelectedTriangles.Length;
			selectedFaceCount += selection[i].SelectedFaceIndices.Length;
			selectedEdgeCount += selection[i].SelectedEdges.Length;

			#if PB_DEBUG
			profiler.BeginSample("Distinct Vertex Count");
			#endif

			int distinctVertexCount = selection[i].sharedIndices.UniqueIndicesWithValues(selection[i].SelectedTriangles).Length;

			#if PB_DEBUG
			profiler.EndSample();
			#endif

			if(distinctVertexCount > per_object_vertexCount_distinct)
				per_object_vertexCount_distinct = distinctVertexCount;

			#if DEBUG
			faceCount += selection[i].faces.Length;
			vertexCount += selection[i].vertexCount;
			triangleCount += selection[i].msh.triangles.Length;
			#endif
		}

		selected_handlePivotWorld = (max+min)/2f;

		#if PB_DEBUG
		profiler.BeginSample("UpdateGraphics");
			UpdateGraphics();
		profiler.EndSample();
		#else
			UpdateGraphics();
		#endif

		#if PB_DEBUG
		profiler.BeginSample("UpdateHandleRotation");
			UpdateHandleRotation();
		profiler.EndSample();
		#else
			UpdateHandleRotation();
		#endif


		#if !PROTOTYPE
		UpdateTextureHandles();
		#endif
		
		currentHandleRotation = handleRotation;

		if(OnSelectionUpdate != null)
			OnSelectionUpdate(selection);

		#if PB_DEBUG
		profiler.EndSample();
		#endif
	}

	// Only updates things that absolutely need to be refreshed, and assumes that no selection changes have occured
	private void Internal_UpdateSelectionFast()
	{
		#if PB_DEBUG
		profiler.BeginSample("Internal_UpdateSelectionFast");
		#endif

		selectedVertexCount = 0;
		selectedFaceCount = 0;
		selectedEdgeCount = 0;

		bool boundsInitialized = false;
		Vector3 min = Vector3.zero, max = Vector3.zero;

		for(int i = 0; i < selection.Length; i++)
		{
			pb_Object pb = selection[i];
			
			transformCache[i] = new Vector3[3]
			{
				pb.transform.position,
				pb.transform.localRotation.eulerAngles,
				pb.transform.localScale
			};
			
			selected_verticesInWorldSpace_all[i] = pb.VerticesInWorldSpace();	// to speed this up, could just get uniqueIndices vertiecs
			selected_verticesLocal_sel[i] = pb.GetVertices(pb.SelectedTriangles);
			// selected_handlePivot[i] = pb_Math.Average(selected_verticesLocal_sel[i]);

			if(selection[i].SelectedTriangleCount > 0)
			{
				if(!boundsInitialized)
				{
					boundsInitialized = true;
					min = selected_verticesInWorldSpace_all[i][selection[i].SelectedTriangles[0]];
					max = min;
				}
				
				for(int n = 0; n < selection[i].SelectedTriangleCount; n++)
				{					
					min = Vector3.Min(min, selected_verticesInWorldSpace_all[i][selection[i].SelectedTriangles[n]]);
					max = Vector3.Max(max, selected_verticesInWorldSpace_all[i][selection[i].SelectedTriangles[n]]);
				}
			}
	
			selectedVertexCount += selection[i].SelectedTriangleCount;
			selectedFaceCount 	+= selection[i].SelectedFaceIndices.Length;
			selectedEdgeCount 	+= selection[i].SelectedEdges.Length;
		}

		selected_handlePivotWorld = (max+min)/2f;
		
		UpdateGraphics();
		UpdateHandleRotation();
		currentHandleRotation = handleRotation;

		if(OnSelectionUpdate != null)
			OnSelectionUpdate(selection);

		#if PB_DEBUG
		profiler.EndSample();
		#endif
	}

	private void UpdateGraphics()
	{
		#if UNITY_4
			pb_Editor_Graphics.UpdateSelectionMesh(selection, selectionMode);
		#else
		if(selectionMode == SelectMode.Face)// && !EditorApplication.isPlaying)
			pb_Editor_Graphics.UpdateSelectionMesh(selection, selectionMode);
		// else
		// 	pb_Editor_Graphics.ClearSelectionMesh();
		#endif
	}

	public void AddToSelection(GameObject t)
	{
		Object[] temp = new Object[Selection.objects.Length + 1];
		temp[0] = t;
		for(int i = 1; i < temp.Length; i++)
			temp[i] = Selection.objects[i-1];
		Selection.objects = temp;
	}

	public void RemoveFromSelection(GameObject t)
	{
		int ind = System.Array.IndexOf(Selection.objects, t);
		if(ind < 0)
			return;

		Object[] temp = new Object[Selection.objects.Length - 1];

		for(int i = 1; i < temp.Length; i++) {
			if(i != ind)
				temp[i] = Selection.objects[i];
		}

		Selection.objects = temp;
	}

	public void Exit()
	{
		pbUndo.RecordObjects(selection, "Change Selection");

		ClearSelection();

		// if the previous tool was set to none, use Tool.Move
		if(Tools.current == Tool.None)
			Tools.current = Tool.Move;

		Selection.activeTransform = null;
	}

	public void Exit(GameObject newSelection)
	{
		pbUndo.RecordObjects(selection, "Change Selection");

		ClearSelection();

		// if the previous tool was set to none, use Tool.Move
		if(Tools.current == Tool.None)
			Tools.current = Tool.Move;

		if(newSelection)
			Selection.activeTransform = newSelection.transform;
		else
			Selection.activeTransform = null;
	}

	public void SetSelection(GameObject[] newSelection)
	{
		pbUndo.RecordObjects(selection, "Change Selection");
		
		ClearSelection();

		// if the previous tool was set to none, use Tool.Move
		if(Tools.current == Tool.None)
			Tools.current = Tool.Move;

		if(newSelection != null && newSelection.Length > 0) {
			Selection.activeTransform = newSelection[0].transform;
			Selection.objects = newSelection;
		}
		else
			Selection.activeTransform = null;
	}

	public void SetSelection(GameObject go)
	{
		pbUndo.RecordObjects(selection, "Change Selection");
		
		ClearSelection();
		AddToSelection(go);
	}

	/**
	 *	Clears all `selected` caches associated with each pb_Object in the current selection.  The means triangles, faces, and edges.
	 */
	public void ClearFaceSelection()
	{
		foreach(pb_Object pb in selection) {
			pb.ClearSelection();
		}

		nearestEdge = null;
		nearestEdgeObjectIndex = -1;
		nearestEdgeIndex = -1;
		
		pb_Editor_Graphics.ClearSelectionMesh();
	}

	public void ClearSelection()
	{
		foreach(pb_Object pb in selection) {
			pb.ClearSelection();
		}

		Selection.objects = new Object[0];

		pb_Editor_Graphics.ClearSelectionMesh();
	}
#endregion

#region HANDLE AND GUI CALCULTATIONS

	#if !PROTOTYPE

	Matrix4x4 handleMatrix = Matrix4x4.identity;

	private void UpdateTextureHandles()
	{
		if(selection.Length < 1) return;

		// Reset temp vars
		textureHandle = selected_handlePivotWorld;
		textureScale = Vector3.one;
		textureRotation = Quaternion.identity;// currentHandleRotation;

		pb_Object pb;
		pb_Face face;

		handleMatrix = selection[0].transform.localToWorldMatrix;

		if( GetFirstSelectedFace(out pb, out face) )
			handleMatrix *= Matrix4x4.TRS( pb_Math.Average( pb.GetVertices(face.distinctIndices) ), handleRotation, Vector3.one);
	}
	#endif


	Vector3 selectedNormal_local = Vector3.up;
	Quaternion handleRotation = new Quaternion(0f, 0f, 0f, 1f);
	public void UpdateHandleRotation()
	{
		Quaternion localRot = Selection.activeTransform == null ? Quaternion.identity : Selection.activeTransform.rotation;

		switch(handleAlignment)
		{
			case HandleAlignment.Plane:

				if( Selection.transforms.Length > 1 )
					goto case HandleAlignment.World;

				pb_Object pb;
				pb_Face face;

				if( !GetFirstSelectedFace(out pb, out face) )
					goto case HandleAlignment.Local;
				else
					selectedNormal_local = pb_Math.Normal( pb.GetVertices(face.indices) );

				// Unity freaks out if SetLookRotation() is Vector3.zero, throwing Debug Logs like crazy - 
				// which in turn slows the editor to a crawl.  This catches that and prevents a zero'd 
				// Vector3 from sneaking through.
				if(selectedNormal_local == Vector3.zero)
					selectedNormal_local = Vector3.up;

				// apply local rotation, then apply rotation derived from normal unit vector
				handleRotation = localRot * Quaternion.LookRotation( selectedNormal_local, Vector3.up );

				break;

			case HandleAlignment.Local:
				handleRotation = localRot;
				break;

			case HandleAlignment.World:
				handleRotation = Quaternion.identity;
				break;
		}
	}
#endregion

#region Selection Management and checks

	private void VerifyTextureGroupSelection()
	{
		foreach(pb_Object pb in selection)
		{
			List<int> alreadyChecked = new List<int>();

			foreach(pb_Face f in pb.SelectedFaces)	
			{
				int tg = f.textureGroup;
				if(tg > 0 && !alreadyChecked.Contains(f.textureGroup))
				{
					foreach(pb_Face j in pb.faces)
						if(j != f && j.textureGroup == tg && !pb.SelectedFaces.Contains(j))
						{
							// int i = EditorUtility.DisplayDialogComplex("Mixed Texture Group Selection", "One or more of the faces selected belong to a Texture Group that does not have all it's member faces selected.  To modify, please either add the remaining group faces to the selection, or remove the current face from this smoothing group.", "Add Group to Selection", "Cancel", "Remove From Group");
							int i = 0;
							switch(i)
							{
								case 0:
									List<pb_Face> newFaceSection = new List<pb_Face>();
									foreach(pb_Face jf in pb.faces)
										if(jf.textureGroup == tg)
											newFaceSection.Add(jf);
									pb.SetSelectedFaces(newFaceSection.ToArray());
									UpdateSelection(false);
									break;

								case 1:
									break;

								case 2:
									f.textureGroup = 0;
									break;
							}
							break;
						}
				}
				alreadyChecked.Add(f.textureGroup);
			}
		}
	}
#endregion

#region EVENTS AND LISTENERS

	/// @todo -- use hasChanged flag (someday)
	Vector3[][] transformCache = new Vector3[0][];

	private void ListenForTopLevelMovement()
	{
		if(selectedVertexCount > 1)// || GUIUtility.hotControl < 1)
			return;

		bool movementDetected = false;
		try {
			for(int i = 0; i < selection.Length; i++)
			{
				if(selection[i] == null)
					continue;

				if(	selection[i].transform.position != transformCache[i][0] ||
					selection[i].transform.localRotation.eulerAngles != transformCache[i][1] ||
					selection[i].transform.localScale != transformCache[i][2])
				{
					movementDetected = true;
					break;
				}
			}
		} catch (System.Exception e) {}

		if(!movementDetected)
			return;

		Internal_UpdateSelectionFast();
	}

	private void OnHierarchyChange()
	{
		#if PB_DEBUG
		profiler.BeginSample("OnHierarchyChange");
		#endif

		bool prefabModified = false;

		if(!EditorApplication.isPlaying && !movingVertices)
		{
			// don't delete, dummy!
			#if PB_DEBUG
			profiler.BeginSample("FindObjectsOfType");
			Object[] objs = FindObjectsOfType(typeof(pb_Object));
			profiler.EndSample();
			foreach(pb_Object pb in objs)
			#else
			foreach(pb_Object pb in FindObjectsOfType(typeof(pb_Object)))
			#endif
			{
				/**
				 * If it's a prefab instance, reconstruct submesh structure.
				 */
				// bool exists = System.Array.Exists(PrefabUtility.GetPropertyModifications(pb.gameObject), x => x.target is MeshRenderer || x.target is MeshFilter);
				// if(	PrefabUtility.GetPrefabType(pb.gameObject) == PrefabType.PrefabInstance && exists )

				#if PB_DEBUG
					profiler.BeginSample("prefab::Verify()");

					if( !pb.Verify() )
					{
						profiler.BeginSample("ToMesh::Prefab");
							pb.ToMesh();
						profiler.EndSample();
						
						profiler.BeginSample("GenerateUV2::Prefab");
							pb.GenerateUV2(show_NoDraw);
						profiler.EndSample();
					}

					profiler.EndSample();
				#else
					if( !pb.Verify() )
					{
						pb.ToMesh();
						pb.GenerateUV2(show_NoDraw);
					}
				#endif
			}
		}

		if(prefabModified)
		{
			#if PB_DEBUG
			profiler.BeginSample("UpdateSelection");
			#endif
			UpdateSelection(true);
			SceneView.RepaintAll();

			#if PB_DEBUG
			profiler.EndSample();
			#endif
		}

		#if PB_DEBUG
		profiler.EndSample();
		#endif
	}

	private void OnSelectionChange()
	{
		nearestEdge = null;
		nearestEdgeIndex = -1;
		nearestEdgeObjectIndex = -1;
		
		if(Selection.objects.Contains(pb_Editor_Graphics.selectionGameObject)) {
			// Debug.LogWarning("TELL KARL THIS WARNING WAS THROWN");
			RemoveFromSelection(pb_Editor_Graphics.selectionGameObject);
		}

		UpdateSelection(false);
	}

	/**
	 * Registered to EditorApplication.onPlaymodeStateChanged
	 */
	private void OnPlayModeStateChanged()
	{		
		if(EditorApplication.isPlaying)
		{
			foreach(pb_Entity entity in FindObjectsOfType(typeof(pb_Entity)))
			{
				switch(entity.entityType)
				{
					case EntityType.Occluder:
					case EntityType.Detail:
						if(!show_Detail)
						{
							entity.transform.GetComponent<MeshRenderer>().enabled = true;
							entity.transform.GetComponent<MeshCollider>().enabled = true;
						}
						break;

					case EntityType.Mover:
						if(!show_Mover)
						{
							entity.transform.GetComponent<MeshRenderer>().enabled = true;
							entity.transform.GetComponent<MeshCollider>().enabled = true;
						}
						break;
	
					case EntityType.Collider:
						if(!show_Collider)
						{
							entity.transform.GetComponent<MeshCollider>().enabled = true;
						}
						break;

					case EntityType.Trigger:
						if(!show_Trigger)
						{
							entity.transform.GetComponent<MeshCollider>().enabled = true;
						}
						break;
				}
			}

		}
		else
		{
			// Turn stuff back off that's not supposed to be on
			foreach(pb_Entity entity in FindObjectsOfType(typeof(pb_Entity)))
			{
				switch(entity.entityType)
				{
					case EntityType.Occluder:
					case EntityType.Detail:
						if(!show_Detail)
						{
							entity.transform.GetComponent<MeshRenderer>().enabled = false;
							entity.transform.GetComponent<MeshCollider>().enabled = false;
						}
						break;

					case EntityType.Mover:
						if(!show_Mover)
						{
							entity.transform.GetComponent<MeshRenderer>().enabled = false;
							entity.transform.GetComponent<MeshCollider>().enabled = false;
						}
						break;

					case EntityType.Collider:
						if(!show_Collider)
						{
							entity.transform.GetComponent<MeshRenderer>().enabled = false;
							entity.transform.GetComponent<MeshCollider>().enabled = false;
						}
						break;

					case EntityType.Trigger:
						if(!show_Trigger)
						{
							entity.transform.GetComponent<MeshRenderer>().enabled = false;
							entity.transform.GetComponent<MeshCollider>().enabled = false;
						}
						break;
				}
			}
		}
	}

	void SceneWideDuplicateCheck()
	{
		pb_Object[] allPBObjects = FindObjectsOfType(typeof(pb_Object)) as pb_Object[];
		foreach(pb_Object pb in allPBObjects)
			pb.Verify();
	}

	void OnValidateCommand(string command)
	{
		switch(command)
		{
			case "UndoRedoPerformed":

				UndoRedoPerformed();

				break;
		}
	}
	
	void UndoRedoPerformed()
	{
		pb_Object[] pbos = pbUtil.GetComponents<pb_Object>(Selection.transforms);

		foreach(pb_Object pb in pbos)
		{
			/**
			 * because undo after subdivide causes verify to fire, the face references aren't the same anymoore - so reset them
			 */
			if( !pb.Verify() && pb.SelectedFaces.Length > 0 )
				pb.SetSelectedFaces( System.Array.FindAll( pb.faces, x => pbUtil.ContainsMatch(x.distinctIndices, pb_Face.AllTriangles(pb.SelectedFaces)) ) );

			pb.ToMesh();
			pb.GenerateUV2(show_NoDraw);
			pb.Refresh();
		
		}

		UpdateSelection(true);
		SceneView.RepaintAll();
	}

	/**
	 *	\brief Used to check whether object is nodraw free or not - matching call is in pb_AutoUV_Editor.SetNoDraw
	 */
	void OnGeometryChanged( pb_Object[] pbs )
	{
		// check the object flags
		foreach(pb_Object pb in pbs)
		{
			StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags( pb.gameObject );
			
			// if nodraw found
			if(pb.containsNodraw)
			{
				if( (flags & StaticEditorFlags.BatchingStatic) == StaticEditorFlags.BatchingStatic )
				{
					flags ^= StaticEditorFlags.BatchingStatic;
					GameObjectUtility.SetStaticEditorFlags(pb.gameObject, flags);
				}
			}
			else
			{
				// if nodraw not found, and entity type should be batching static
				if(pb.GetComponent<pb_Entity>().entityType != EntityType.Mover)
				{
					flags = flags | StaticEditorFlags.BatchingStatic;
					GameObjectUtility.SetStaticEditorFlags(pb.gameObject, flags);
				}
			}
		}
	}

	private void PushToGrid(float snapVal)
	{
		for(int i = 0; i  < selection.Length; i++)
		{
			pb_Object pb = selection[i];

			int[] indices = pb.sharedIndices.AllIndicesWithValues(pb.SelectedTriangles);

			Vector3[] verts = pb.vertices;
			
			for(int n = 0; n < indices.Length; n++)
				verts[indices[n]] = pb.transform.InverseTransformPoint(pbUtil.SnapValue(pb.transform.TransformPoint(verts[indices[n]]), Vector3.one, snapVal));
				
			// don't bother calling a full ToMesh() here because we know for certain that the _vertices and msh.vertices arrays are equal in length
			pb.SetVertices(verts);
			pb.msh.vertices = verts;

			pb.RefreshUV( SelectedFacesInEditZone[i] );
			pb.RefreshNormals();
		}

		Internal_UpdateSelectionFast();
	}

	/**
	 *	A tool, any tool, has just been engaged
	 */
	public void OnBeginTextureModification()
	{
		VerifyTextureGroupSelection();
	}

	public void OnFinishedVertexModification()
	{	
		if(OnVertexMovementFinished != null)
			OnVertexMovementFinished(selection);

		currentHandleScale = Vector3.one;
		currentHandleRotation = handleRotation;

		#if !PROTOTYPE
		if(movingPictures)
		{
			if(pb_UV_Editor.instance != null)
				pb_UV_Editor.instance.OnFinishUVModification();

			UpdateTextureHandles();

			movingPictures = false;
		}
		else
		#endif
		if(movingVertices)
		{
			foreach(pb_Object sel in selection)
			{
				sel.Refresh();
				sel.GenerateUV2(show_NoDraw);
			}
			movingVertices = false;
		}

		scaling = false;
	}
#endregion

#region WINDOW MANAGEMENT
	
	public void OpenGeometryInterface()
	{
		EditorWindow.GetWindow(typeof(pb_Geometry_Interface), true, "Shape Tool", true);
	}
#endregion

#region DEBUG

	void DrawVertexNormals(float dist)
	{
		if(dist <= Mathf.Epsilon) return;

		foreach(pb_Object pb in selection)
		{
			// selection doesn't update fast enough, so this null check needs to exist
			if(pb == null)
				continue;

			Vector3[] verts = pb.VerticesInWorldSpace();
			Vector3[] nrmls = pb.msh.normals;

			for(int i = 0; i < verts.Length; i++)
			{
				float angle = 0f;
				Vector3 vup = verts[i];
				pb.transform.localRotation.ToAngleAxis(out angle, out vup);

				Vector3 v0 = verts[i];
				Vector3 v1 = verts[i] + Quaternion.AngleAxis(angle, vup) * pb.msh.normals[i] * dist;

				Handles.color = new Color( Mathf.Abs(nrmls[i].x), Mathf.Abs(nrmls[i].y), Mathf.Abs(nrmls[i].z) );
					Handles.DrawLine(v0, v1);

				Handles.color = Color.white;
			}
		}
	}

	const float NRML_LENGTH = .3f;

	public void DrawFaceNormals()
	{
		foreach(pb_Object pb in selection)
		{
			// selection doesn't update fast enough, so this null check needs to exist
			if(pb == null)
				continue;

			pb_Face[] faces = pb.SelectedFaces;

			for(int i = 0; i < faces.Length; i++)
			{
				Vector3[] fv = pb.GetVertices(faces[i]);
				Vector3 v = pb.transform.TransformPoint(pb_Math.Average(fv));
				Vector3 nrml = pb.transform.TransformDirection(pb_Math.Normal(fv));

				Handles.color = Color.green;
					Handles.DrawLine(v, v + nrml);
				Handles.color = Color.white;
			}
		}
	}
#endregion

#region CONVENIENCE CALLS

	/**
	 * Returns the first selected pb_Object and pb_Face, or false if not found.
	 */
	public bool GetFirstSelectedFace(out pb_Object pb, out pb_Face face)
	{
		pb = null;
		face = null;

		if(selection.Length < 1) return false;
		
		pb = selection.FirstOrDefault(x => x.SelectedFaceIndices.Length > 0);
		
		if(pb == null)
			return false;
		
		face = pb.faces[pb.SelectedFaceIndices[0]];

		return true;
	}

	/**
	 * Returns the first selected pb_Object and pb_Face, or false if not found.
	 */
	public bool GetFirstSelectedMaterial(ref Material mat)
	{
		for(int i = 0; i < selection.Length; i++)
		{
			for(int n = 0; n < selection[i].SelectedFaceIndices.Length; n++)
			{
				if(selection[i].faces[selection[i].SelectedFaceIndices[n]].material != null)
				{
					mat = selection[i].faces[selection[i].SelectedFaceIndices[n]].material;
					return true;
				}
			}
		}
		return false;
	}

	// Handy calls -- currentEvent must be set, so only call in the OnGUI loop!
	public bool altClick 			{ get { return (currentEvent.alt); } }
	public bool leftClick 			{ get { return (currentEvent.type == EventType.MouseDown); } }
	public bool leftClickUp 		{ get { return (currentEvent.type == EventType.MouseUp); } }
	public bool contextClick 		{ get { return (currentEvent.type == EventType.ContextClick); } }
	public bool mouseDrag 			{ get { return (currentEvent.type == EventType.MouseDrag); } }
	public bool ignore 				{ get { return currentEvent.type == EventType.Ignore; } }
	public Vector2 mousePosition 	{ get { return currentEvent.mousePosition; } }
	public Vector2 eventDelta 		{ get { return currentEvent.delta; } }
	public bool rightClick 			{ get { return (currentEvent.type == EventType.ContextClick); } }
	public bool shiftKey 			{ get { return currentEvent.shift; } }
	public bool ctrlKey 			{ get { return currentEvent.command || currentEvent.control; } }
	public KeyCode getKeyUp			{ get { return currentEvent.type == EventType.KeyUp ? currentEvent.keyCode : KeyCode.None; } }
#endregion

}