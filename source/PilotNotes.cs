using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

namespace PilotNotes
{
	/*
	 * @name PilotNotes
	 * @author David Kirkbride <dk AT whoshotdk DOT co DOT uk>
	 * 
	 * With many thanks to the kind people at IRC channel #kspmodders who helped me overcome some bugs;
	 * 
	 * darklight
	 * mobilefoxy
	 * taniwha
	 * thomas
	 */
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class PilotNotes : MonoBehaviour
	{
		private static string MOD_VERSION = "060920152030";
		private string modName = "PilotNotes";

		private string pathToKSP = new DirectoryInfo(KSPUtil.ApplicationRootPath).FullName;

		/// <summary>
		/// The craftData dictionary stores CID/description pairs of vessels loaded from .craft files
		/// </summary>
		private Dictionary<string, string> craftData = new Dictionary<string, string>();

		/// <summary>
		/// The vesselData dictionary stores CID/description pairs of in-game vessels.
		/// </summary>
		private Dictionary<string, string> vesselData = new Dictionary<string, string>();

		private static float WINDOW_WIDTH = 256f;
		private static float WINDOW_HEIGHT = 312f;

		private Rect windowRect = new Rect(99999f, 38f, WINDOW_WIDTH, WINDOW_HEIGHT);
		private Vector2 windowScrollPosition;
		private ApplicationLauncherButton toolbarButton;
		
		private string currentVesselName = "";
		private string currentVesselDescription = "";
		
		/****************************************************************************************************
		 * EVENTS
		 ****************************************************************************************************/

		/// <summary>
		/// Called when the PilotNotes module is loaded. Set up event listeners.
		/// </summary>
		public void Awake() {

			logDebug ("Version " + MOD_VERSION);

			logDebug ("Adding event listeners.");
			GameEvents.onVesselChange.Add (onVesselChange);
			
			GameEvents.onGUIApplicationLauncherReady.Add (onToolbarReady);
			GameEvents.onGUIApplicationLauncherDestroyed.Add (onToolbarDestroy);
			
			DontDestroyOnLoad (this);
		}

		/// <summary>
		/// Called when the game is leaving a scene. Remove event listeners.
		/// </summary>
		public void OnDestroy() {
			
			logDebug ("Removing event listeners.");
			GameEvents.onVesselChange.Remove (onVesselChange);
			
			GameEvents.onGUIApplicationLauncherReady.Remove (onToolbarReady);
			GameEvents.onGUIApplicationLauncherDestroyed.Remove (onToolbarDestroy);
		}

		/// <summary>
		/// Called when the application toolbar is ready to be modified. Create the PilotNotes toolbar button and set its event listeners.
		/// </summary>
		private void onToolbarReady() {
			
			logDebug("Adding toolbar button.");
			
			Texture2D btnTexture = GameDatabase.Instance.GetTexture("PilotNotes/ToolbarIcon", false);

			if (!toolbarButton) {

				toolbarButton = ApplicationLauncher.Instance.AddModApplication (showWindow, hideWindow, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, btnTexture);
			}
		}

		/// <summary>
		/// Called when the application toolbar is destroyed. Remove the PilotNotes toolbar button and hide the PilotNotes window.
		/// </summary>
		private void onToolbarDestroy() {
			
			ApplicationLauncher.Instance.RemoveModApplication (toolbarButton);
			toolbarButton = null;

			hideWindow();
		}

		/// <summary>
		/// Called when the active vessel is switched. Load craftData and vesselData dictionaries and fetch the freshly activated vessel's description.
		/// </summary>
		private void onVesselChange(Vessel _vessel) {
			
			loadDictionaries();
			fetchVesselDescription (_vessel);
		}
		
		/****************************************************************************************************
		 * VIEW / WINDOW STUFF
		 ****************************************************************************************************/

		/// <summary>
		/// Shows the PilotNotes window.
		/// </summary>
		private void showWindow() {
			
			logDebug ("Drawing window.");
			RenderingManager.AddToPostDrawQueue(0, handleWindow);
		}

		/// <summary>
		/// Hides the PilotNotes window.
		/// </summary>
		private void hideWindow() {
			
			logDebug ("Removing window.");
			RenderingManager.RemoveFromPostDrawQueue (0, handleWindow);
		}

		/// <summary>
		/// Called when the game is ready to draw the window. Clamp the window to the screen, and set it's 'draw' event listener
		/// </summary>
		private void handleWindow() {
			
			windowRect = KSPUtil.ClampRectToScreen(GUILayout.Window(GetInstanceID(), windowRect, drawWindow, modName + ": " + currentVesselName, HighLogic.Skin.window));
		}

		/// <summary>
		/// Draws the window  contents.
		/// </summary>
		/// <param name="_windowID">The unique identifier of the PilotNotes window</param>
		private void drawWindow(int _windowID) {

			windowScrollPosition = GUILayout.BeginScrollView(windowScrollPosition, GUILayout.Width(WINDOW_WIDTH), GUILayout.Height(WINDOW_HEIGHT));
			GUILayout.Label(currentVesselDescription.Replace ("¨", "\n"));
			GUILayout.EndScrollView();

			// Allow the window to be draggable from anywhere within it
			GUI.DragWindow ();
		}

		/****************************************************************************************************
		 * VESSEL DATA STUFF
		 ****************************************************************************************************/

		/// <summary>
		/// Load craft data from .craft files and in-game vessels. This will populate the vesselData dictionary with pairs of CIDs => Descriptions. Although the CID belongs to a part, it is used to uniquely identify a vessel.
		/// </summary>
		private void loadDictionaries() {
			
			// Clear dictionaries (in case we run this code more than once)
			craftData.Clear ();
			vesselData.Clear ();
			
			logDebug ("=====================");
			logDebug ("Scanning .craft files...");
			
			// Grab all craft files
			string pathToVABcraftFiles = pathToKSP + "saves/" + HighLogic.SaveFolder + "/Ships/VAB/";
			string pathToSPHcraftFiles = pathToKSP + "saves/" + HighLogic.SaveFolder + "/Ships/SPH/";
			
			scanCraftFiles (pathToVABcraftFiles);
			scanCraftFiles (pathToSPHcraftFiles);
			
			// Now that we have all craft file data stored; lets grab the in-game vessels
			scanSFSvessels ();
			
			// At this point, vesselData contains in-game vessel CID's and their matching descriptions
		}

		/// <summary>
		/// Fetches the vessel description from the vesselData dictionary.
		/// </summary>
		/// <param name="_vessel">The vessel to get the description for.</param>
		private void fetchVesselDescription(Vessel _vessel) {
			
			logDebug("=====================");
			logDebug ("Fetching particular vessel description...");
			
			// Grab the vessel's root part CID
			string vesselCID = _vessel.rootPart.craftID.ToString();
			
			if (String.IsNullOrEmpty (vesselCID)) {
				
				logDebug ("CID is NULL or cannot be parsed.");
				return;
			}
			
			logDebug("CID is " + vesselCID + ".");

			currentVesselName = _vessel.GetName ();
			logDebug("Name is " + currentVesselName + ".");

			// Find matching CID in vessel dictionary
			if (!vesselData.ContainsKey (vesselCID)) {
				
				logDebug("Matching CID in vessel dictionary not found.");

				currentVesselDescription = "No description can be displayed as the original .craft file could not be located for this vessel.";

				return;
			}

			currentVesselDescription = vesselData[vesselCID].ToString();

			if (String.IsNullOrEmpty (currentVesselDescription)) {

				currentVesselDescription = "This vessel does not have a description.";
			}

			logDebug("Description is " + currentVesselDescription + ".");
		}
		
		/// <summary>
		/// Scans the .craft files and populates the craftData dictionary
		/// </summary>
		/// <param name="_path">The full path to the directory of craft files.</param>
		private void scanCraftFiles(string _path) {
			
			string[] craftFiles = Directory.GetFiles(_path, "*.craft");
			
			// Iterate crafts
			foreach (string craftFilePath in craftFiles) {
				
				logDebug("---------------------");
				
				if(String.IsNullOrEmpty(craftFilePath)) {
					
					logDebug ("Filename is NULL.");
					continue;
				}
				
				string craftFileName = craftFilePath.Replace (_path, "");
				logDebug ("Filename: " + craftFileName + ".");
				
				if(craftFileName == "Auto-Saved Ship.craft") {
					
					logDebug ("Ignoring auto saved craft.");
					continue;
				}
				
				// Load the .craft file into a ConfigNode
				ConfigNode craftGlobalNode = ConfigNode.Load(craftFilePath);
				
				if(craftGlobalNode == null) {
					
					logDebug ("Cannot create ConfigNode");
					continue;
				}
				
				// Grab craft root part node
				ConfigNode craftRootPartNode = craftGlobalNode.GetNodes("PART")[0];
				
				if(craftRootPartNode == null) {
					
					logDebug ("Cannot get root part ConfigNode.");
					continue;
				}
				
				// Parse CID
				string craftCID = craftRootPartNode.GetValue("part");
				
				if(String.IsNullOrEmpty(craftCID)) {
					
					logDebug ("CID is NULL or cannot be parsed.");
					continue;
				}
				
				craftCID = craftCID.Substring(craftCID.LastIndexOf("_") + 1);
				logDebug("CID is " + craftCID + ".");
				
				// Parse description
				string craftDescription = craftGlobalNode.GetValue("description");
				
				if(String.IsNullOrEmpty(craftDescription)) {
					
					craftDescription = "";
					logDebug ("Description is NULL or empty.");
				} else {
					
					logDebug("Description is " + craftDescription + ".");
				}
				
				// Add to craft dictionary
				if(craftData.ContainsKey (craftCID)) {
					
					logDebug ("Craft dictionary already contains this craft.");
					continue;
				}
				
				craftData.Add (craftCID, craftDescription);
				logDebug ("Added craft to craft dictionary.");
			}
		}
		
		/// <summary>
		/// Scans the in-game vessels and if a matching craft in the craftData dictionary is found, add a new entry to the vesselData dictionary
		/// </summary>
		private void scanSFSvessels() {
			
			logDebug("=====================");
			logDebug ("Scanning in-game vessels...");
			
			List<Vessel> vessels = FlightGlobals.Vessels;
			
			foreach (Vessel vessel in vessels) {
				
				logDebug("---------------------");
				
				// Grab vessel root part node
				Part vesselRootPart = vessel.rootPart;
				
				if(vesselRootPart == null) {
					
					logDebug ("Cannot get root part.");
					continue;
				}
				
				// Parse vessel CID
				string vesselCID = vesselRootPart.craftID.ToString();
				
				if(String.IsNullOrEmpty(vesselCID)) {
					
					logDebug ("CID is NULL or cannot be parsed.");
					continue;
				}
				
				logDebug("CID is " + vesselCID + ".");
				
				// Find matching CID in craft dictionary
				if (!craftData.ContainsKey(vesselCID)) {
					
					logDebug("Matching CID in craft dictionary not found.");
					continue;
				}
				
				if(vesselData.ContainsKey (vesselCID)) {
					
					logDebug ("Vessel dictionary already contains this vessel.");
					continue;
				}
				
				vesselData.Add (vesselCID, craftData[vesselCID].ToString());
				logDebug ("Added vessel to vessel dictionary.");
			}
		}

		/****************************************************************************************************
		 * UTILITIES
		 ****************************************************************************************************/

		/// <summary>
		/// Add something to the debug window log
		/// </summary>
		/// <param name="_string">The message to add to the log</param>
		private void logDebug(string _string) {
			
			print ("[" + modName + "] " + _string);
		}
	}
}

