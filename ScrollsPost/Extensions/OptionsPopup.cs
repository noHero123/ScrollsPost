using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ScrollsPost {
    public class OptionPopups : Popups {
        public class ConfigOption {
            public String text;
            public object key;
            public Boolean enabled;

            public ConfigOption(String text, object key, Boolean enabled = false) {
                this.text = text;
                this.key = key;
                this.enabled = enabled;
            }
        }

        private enum PopupType { NONE, MULTI_SCROLL, REPLAY_SCROLL }

        private Vector2 optionScroll = Vector2.zero;
        private GUISkin regularUISkin;
        private GUIBlackOverlayButton overlay;
        private GUIStyle highlightedButtonStyle;

        private List<ConfigOption> configOptions;

        private PopupType currentPopupType;

        private ICancelCallback cancelCallback;
        private IOkStringCallback okStringCallback;

        private string header;
        private string description;
        private string cancelText;
        private string okText;
        private string popupType;

        private void Start() {
            if( this.overlay != null )
                return;

            this.overlay = new GameObject("PopupBlackOverlay").AddComponent<GUIBlackOverlayButton>();
            this.overlay.Init(this, 5, false);
            this.overlay.enabled = false;
            UnityEngine.Object.DontDestroyOnLoad(this.overlay.gameObject);

            this.currentPopupType = PopupType.NONE;

            this.regularUISkin = (GUISkin)Resources.Load("_GUISkins/RegularUI");

            this.highlightedButtonStyle = new GUIStyle(this.regularUISkin.button);
            this.highlightedButtonStyle.normal.background = this.highlightedButtonStyle.hover.background;
        }

        private void ShowPopup(PopupType type) {
            Start();
            this.currentPopupType = type;
            this.overlay.enabled = true;
        }

        public void ShowMultiScrollPopup(IOkStringCancelCallback callback, String popupType, String header, String description, List<ConfigOption> configOptions) {
            this.ShowPopup(PopupType.MULTI_SCROLL);

            this.popupType = popupType;
            this.optionScroll = Vector2.zero;

            this.okStringCallback = callback;
            this.cancelCallback = callback;

            this.configOptions = configOptions;

            this.header = header;
            this.description = description;
            this.cancelText = "Done";
            this.okText = popupType.Equals("main") ? null : "Back";
        }

        public void ShowReplayScrollPopup(IOkStringCancelCallback callback, String popupType, String header, String description, List<ConfigOption> configOptions) {
            this.ShowPopup(PopupType.REPLAY_SCROLL);

            this.popupType = popupType;
            this.optionScroll = Vector2.zero;

            this.okStringCallback = callback;
            this.cancelCallback = callback;

            this.configOptions = configOptions;

            this.header = header;
            this.description = description;
            this.cancelText = "Done";
            this.okText = "Play";
        }

        private void OnGUI() {
            if( this.currentPopupType == PopupType.NONE )
                return;

            GUI.depth = 4;
            GUI.skin = this.regularUISkin;
            int fontSize = GUI.skin.button.fontSize;
            GUI.skin.button.fontSize = 10 + Screen.height / 72;
            float num = (float)Screen.height * 0.03f;
            Rect rect;
            if( this.currentPopupType == PopupType.REPLAY_SCROLL ) {
                rect = new Rect((float)Screen.width * 0.50f - (float)Screen.height * 0.50f, (float)Screen.height * 0.10f, (float)Screen.height * 1.05f, (float)Screen.height * 0.70f);
            } else {
                rect = new Rect((float)Screen.width * 0.50f - (float)Screen.height * 0.40f, (float)Screen.height * 0.15f, (float)Screen.height * 0.8f, (float)Screen.height * 0.6f);
            }

            Rect rect2 = new Rect(rect.x + num, rect.y + num, rect.width - 2f * num, rect.height - 2f * num);
            new ScrollsFrame(rect).AddNinePatch(ScrollsFrame.Border.LIGHT_CURVED, NinePatch.Patches.CENTER).Draw();

            float num4 = (float)Screen.height * 0.055f;
            int fontSize3 = GUI.skin.label.fontSize;
            GUI.skin.label.fontSize = 14 + Screen.height / 32;
            GUI.Label(new Rect(rect2.x, rect2.y, rect2.width, num4), this.header);
            GUI.skin.label.fontSize = fontSize3;

            if( !String.IsNullOrEmpty(this.description) ) {
                bool wordWrap = GUI.skin.label.wordWrap;
                GUI.skin.label.wordWrap = true;
                GUI.skin.label.fontSize = 10 + Screen.height / 60;
                GUI.Label(new Rect(rect2.x, rect2.y + (num4 * 0.80f), rect2.width, num4), this.description);
                GUI.skin.label.fontSize = fontSize3;
                GUI.skin.label.wordWrap = wordWrap;
            }

            if( this.currentPopupType == PopupType.MULTI_SCROLL || this.currentPopupType == PopupType.REPLAY_SCROLL ) {
                this.DrawMultiScroll(rect2);
            }


            GUI.skin.button.fontSize = fontSize;
        }

        private void HidePopup() {
            this.currentPopupType = PopupType.NONE;
            this.overlay.enabled = false;
        }

        private bool GUIButton(Rect r, string text, Boolean highlight=false) {
            if( GUI.Button(r, text, highlight ? this.highlightedButtonStyle : this.regularUISkin.button) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                return true;
            }

            return false;
        }

        private void DrawMultiScroll(Rect popupInner) {
            Rect position = new Rect(popupInner.x, popupInner.y + popupInner.height * 0.20f, popupInner.width, popupInner.height * 0.70f);
            float num = (float)Screen.height * 0.015f;
            float num2 = (float)Screen.height * 0.07f;
            float num3 = num2 + num;
            Rect position2 = new Rect(position.x + 2f + num, position.y + 2f + num, position.width - 4f - 2f * num, position.height - 4f - 2f * num);
            float num4 = position2.width - 20f;
            int num5 = (this.configOptions.Count % 2 != 0) ? (this.configOptions.Count / 2 + 1) : (this.configOptions.Count / 2);

            int fontSize = GUI.skin.label.fontSize;
            int fontSize2 = GUI.skin.button.fontSize;
            bool wordWrap = GUI.skin.label.wordWrap;

            GUI.skin.label.wordWrap = false;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            GUI.Box(position, string.Empty);
            GUI.Box(new Rect(position2.xMax - 15f, position2.y, 15f, position2.height), string.Empty);
            GUI.color = Color.white;

            this.optionScroll = GUI.BeginScrollView(position2, this.optionScroll, new Rect(0f, 0f, num4, (float)(num5 - 1) * num3 + num2));

            float per_row = this.currentPopupType == PopupType.MULTI_SCROLL ? 2f : 3f;
            for( int i = 0; i < num5; i++ ) {
                for( int j = 0; j < per_row; j++ ) {
                    if( per_row * i + j < this.configOptions.Count ) {
                        ConfigOption option = this.configOptions[(int)per_row * i + j];
                        Rect r = new Rect((float)j * num4 / per_row, (float)i * num3, num4 / per_row - num, num2);
                        GUI.skin.button.fontSize = 10 + Screen.height / 60;
                        if( this.GUIButton(r, option.text + (option.enabled ? " (*)" : string.Empty), option.enabled) && !option.enabled ) {
                            // Select the option so the user sees the change
                            foreach( ConfigOption opt in this.configOptions ) {
                                opt.enabled = false;
                            }

                            option.enabled = true;

                            // And push it to the callback
                            this.okStringCallback.PopupOk(this.popupType, option.key.ToString());
                        }
                    }
                }
            }

            GUI.EndScrollView();

            if( this.currentPopupType == PopupType.REPLAY_SCROLL ) {
                GUI.skin.label.fontSize = 8 + Screen.height / 72;
                Rect r2 = new Rect(popupInner.xMax - (float)Screen.height * 0.1f, popupInner.yMax - (float)Screen.height * 0.04f, (float)Screen.height * 0.1f, (float)Screen.height * 0.05f);
                GUI.skin.button.fontSize = 10 + Screen.height / 60;

                if( this.GUIButton(r2, "Done") ) {
                    this.HidePopup();
                    this.cancelCallback.PopupCancel(this.popupType);
                }

                Rect r3 = new Rect(popupInner.xMax - popupInner.width, popupInner.yMax - (float)Screen.height * 0.04f, (float)Screen.height * 0.06f, (float)Screen.height * 0.05f);
                if( this.GUIButton(r3, "Play") ) {
                    this.HidePopup();
                    this.okStringCallback.PopupOk(this.popupType, "play");
                }

                r3 = new Rect(r3.x + r3.width + 14f, r3.y, r3.width * 1.8f, r3.height);
                if( this.GUIButton(r3, "Play File") ) {
                    this.HidePopup();
                    this.okStringCallback.PopupOk(this.popupType, "play-file");
                }

                r3 = new Rect(r3.x + r3.width + 14f, r3.y, r3.width, r3.height);
                if( this.GUIButton(r3, "Play URL") ) {
                    this.HidePopup();
                    this.okStringCallback.PopupOk(this.popupType, "play-url");
                }

                r3 = new Rect(r3.x + r3.width + 40f, r3.y, (float)Screen.height * 0.08f, r3.height);
                if( this.GUIButton(r3, "Upload") ) {
                    this.HidePopup();
                    this.okStringCallback.PopupOk(this.popupType, "upload");
                }

            } else {
                GUI.skin.label.fontSize = 8 + Screen.height / 72;
                Rect r2 = new Rect(popupInner.xMax - (float)Screen.height * 0.2f, popupInner.yMax - (float)Screen.height * 0.04f, (float)Screen.height * 0.2f, (float)Screen.height * 0.05f);
                GUI.skin.button.fontSize = 10 + Screen.height / 60;

                if( this.GUIButton(r2, this.cancelText) ) {
                    this.HidePopup();
                    this.cancelCallback.PopupCancel(this.popupType);
                }

                if( !String.IsNullOrEmpty(this.okText) ) {
                    Rect r3 = new Rect(popupInner.xMax - (float)Screen.height * 0.740f, popupInner.yMax - (float)Screen.height * 0.04f, (float)Screen.height * 0.2f, (float)Screen.height * 0.05f);
                    if( this.GUIButton(r3, this.okText) ) {
                        this.HidePopup();
                        this.okStringCallback.PopupOk("back", "back");
                    }
                }
            }

            GUI.skin.button.fontSize = fontSize2;
            GUI.skin.label.fontSize = fontSize;
            GUI.skin.label.wordWrap = wordWrap;
        }
    }
}