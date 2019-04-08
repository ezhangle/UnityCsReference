// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;

namespace UnityEditor
{
    internal class GenericInspector : Editor
    {
        private enum OptimizedBlockState
        {
            CheckOptimizedBlock,
            HasOptimizedBlock,
            NoOptimizedBlock
        }

        private AudioFilterGUI m_AudioFilterGUI;

        private float m_LastHeight;
        private OptimizedBlockState m_OptimizedBlockState = OptimizedBlockState.CheckOptimizedBlock;

        internal override bool GetOptimizedGUIBlock(bool isDirty, bool isVisible, out float height)
        {
            height = -1;

            // Don't use optimizedGUI for audio filters
            var behaviour = target as MonoBehaviour;
            if (behaviour != null && AudioUtil.HasAudioCallback(behaviour) && AudioUtil.GetCustomFilterChannelCount(behaviour) > 0)
                return false;

            if (ObjectIsMonoBehaviourOrScriptableObject(target))
                return false;

            if (isDirty)
                ResetOptimizedBlock();

            if (!isVisible)
            {
                height = 0;
                return true;
            }

            // Return cached result if any.
            if (m_OptimizedBlockState != OptimizedBlockState.CheckOptimizedBlock)
            {
                if (m_OptimizedBlockState == OptimizedBlockState.NoOptimizedBlock)
                    return false;
                height = m_LastHeight;
                return true;
            }

            // Update serialized object representation
            if (m_SerializedObject == null)
                m_SerializedObject = new SerializedObject(targets, m_Context) {inspectorMode = inspectorMode};
            else
            {
                m_SerializedObject.Update();
                m_SerializedObject.inspectorMode = inspectorMode;
            }

            height = 0;
            SerializedProperty property = m_SerializedObject.GetIterator();
            while (property.NextVisible(height <= 0))
            {
                var handler = ScriptAttributeUtility.GetHandler(property);
                if (!handler.CanCacheInspectorGUI(property))
                    return ResetOptimizedBlock(OptimizedBlockState.NoOptimizedBlock);

                // Allocate height for control plus spacing below it
                height += handler.GetHeight(property, null, true) + EditorGUI.kControlVerticalSpacing;
            }

            // Allocate height for spacing above first control
            if (height > 0)
                height += EditorGUI.kControlVerticalSpacing;

            m_LastHeight = height;
            m_OptimizedBlockState = OptimizedBlockState.HasOptimizedBlock;
            return true;
        }

        internal override bool OnOptimizedInspectorGUI(Rect contentRect)
        {
            bool childrenAreExpanded = true;
            bool wasEnabled = GUI.enabled;
            var visibleRect = GUIClip.visibleRect;
            var contentOffset = contentRect.y;
            contentRect.y += EditorGUI.kControlVerticalSpacing;

            var property = m_SerializedObject.GetIterator();
            var isInspectorModeNormal = inspectorMode == InspectorMode.Normal;
            while (property.NextVisible(childrenAreExpanded))
            {
                var handler = ScriptAttributeUtility.GetHandler(property);
                contentRect.height = handler.GetHeight(property, null, false);

                if (contentRect.Overlaps(visibleRect))
                {
                    EditorGUI.indentLevel = property.depth;
                    using (new EditorGUI.DisabledScope(isInspectorModeNormal && "m_Script" == property.propertyPath))
                        childrenAreExpanded = handler.OnGUI(contentRect, property, null, false, visibleRect);
                }
                else
                    childrenAreExpanded = property.isExpanded;

                contentRect.y += contentRect.height + EditorGUI.kControlVerticalSpacing;
            }

            m_LastHeight = contentRect.y - contentOffset;
            GUI.enabled = wasEnabled;
            return m_SerializedObject.ApplyModifiedProperties();
        }

        public bool MissingMonoBehaviourGUI()
        {
            serializedObject.Update();
            SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
            if (scriptProperty == null)
                return false;

            bool scriptLoaded = CheckIfScriptLoaded(scriptProperty);
            bool oldGUIEnabled = GUI.enabled;
            if (!GUI.enabled && !scriptLoaded)
            {
                GUI.enabled = true;
            }

            EditorGUILayout.PropertyField(scriptProperty);

            if (!scriptLoaded)
            {
                ShowScriptNotLoadedWarning();
            }

            GUI.enabled = oldGUIEnabled;

            if (serializedObject.ApplyModifiedProperties())
                EditorUtility.ForceRebuildInspectors();

            return true;
        }

        private static bool CheckIfScriptLoaded(SerializedProperty scriptProperty)
        {
            MonoScript targetScript = scriptProperty?.objectReferenceValue as MonoScript;
            return targetScript != null && targetScript.GetScriptTypeWasJustCreatedFromComponentMenu();
        }

        private static void ShowScriptNotLoadedWarning()
        {
            var text = L10n.Tr(
                "The associated script can not be loaded.\nPlease fix any compile errors\nand assign a valid script.");
            EditorGUILayout.HelpBox(text, MessageType.Warning, true);
        }

        internal static void ShowScriptNotLoadedWarning(SerializedProperty scriptProperty)
        {
            bool scriptLoaded = CheckIfScriptLoaded(scriptProperty);
            if (!scriptLoaded)
            {
                ShowScriptNotLoadedWarning();
            }
        }

        private bool ResetOptimizedBlock(OptimizedBlockState resetState = OptimizedBlockState.CheckOptimizedBlock)
        {
            m_LastHeight = -1;
            m_OptimizedBlockState = resetState;
            return m_OptimizedBlockState == OptimizedBlockState.HasOptimizedBlock;
        }

        internal void OnDisableINTERNAL()
        {
            ResetOptimizedBlock();
            CleanupPropertyEditor();
        }

        internal static bool ObjectIsMonoBehaviourOrScriptableObject(Object obj)
        {
            if (obj) // This test for native reference state first.
            {
                return obj.GetType() == typeof(MonoBehaviour) || obj.GetType() == typeof(ScriptableObject);
            }
            return obj is MonoBehaviour || obj is ScriptableObject;
        }

        public override void OnInspectorGUI()
        {
            if (ObjectIsMonoBehaviourOrScriptableObject(target) && MissingMonoBehaviourGUI())
                return;

            base.OnInspectorGUI();

            var behaviour = target as MonoBehaviour;
            if (behaviour != null)
            {
                // Does this have a AudioRead callback?
                if (AudioUtil.HasAudioCallback(behaviour) && AudioUtil.GetCustomFilterChannelCount(behaviour) > 0)
                {
                    if (m_AudioFilterGUI == null)
                        m_AudioFilterGUI = new AudioFilterGUI();
                    m_AudioFilterGUI.DrawAudioFilterGUI(behaviour);
                }
            }
        }
    }
}
