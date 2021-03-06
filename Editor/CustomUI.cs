using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEngine;


namespace PerfHammer
{
    public static class GUIStyles
    {
        //public static readonly GUIStyle Header = EditorResources.GetStyle("sb-settings-header");
        public static GUIStyle Header {
            get {
                if (_header == null) {
                    _header = new GUIStyle("ShurikenModuleTitle") {
                        font = new GUIStyle(EditorStyles.boldLabel).font,
                        fontSize = GUI.skin.font.fontSize,
                        border = new RectOffset(15, 7, 4, 4),
                        fixedHeight = 22,
                        contentOffset = new Vector2(20f, -2f)
                    };
                }
                return _header;
            }
        }

        public static GUIStyle AsciiArt {
            get {
                if (_monospace == null) {
                    _monospace = new GUIStyle(EditorStyles.label) {
                        fontSize = 16,
                        font = AssetDatabase.LoadAssetAtPath("Packages/com.naraenda.perfhammer/Editor/Fonts/november.ttf", typeof(Font)) as Font,
                        normal = new GUIStyleState() {
                            textColor = Color.white,
                        },
                        wordWrap = false,
                    };
                }
                return _monospace;
            }
        }

        public static GUIStyle FoldIcon {
            get {
                if (_justFoldIcon == null || true) {
                    _justFoldIcon = new GUIStyle(EditorStyles.foldoutHeader) {
                        fixedWidth = 0.5f,
                    };
                }
                return _justFoldIcon;
            }
        }

        private static GUIStyle _header;

        private static GUIStyle _monospace;

        private static GUIStyle _justFoldIcon;
    }

    public static class CustomUI
    {
        public static void Section(string title, ref bool display, Action show) {
            var rect = GUILayoutUtility.GetRect(16f, 22f, GUIStyles.Header);
            GUI.Box(rect, title, GUIStyles.Header);

            var e = Event.current;

            var toggleRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
            if (e.type == EventType.Repaint) {
                EditorStyles.foldout.Draw(toggleRect, false, false, display, false);
            }

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition)) {
                display = !display;
                e.Use();
            }

            if (display) {
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.BeginVertical();
                show();
                GUILayout.EndVertical();
                GUILayout.Space(20);
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
        }

        private static Rect GetLinePositionFrom(Rect rect, int line) {
            return new Rect(
                rect.x,
                rect.y,
                rect.width,
                EditorGUIUtility.singleLineHeight);
        }
    }
}