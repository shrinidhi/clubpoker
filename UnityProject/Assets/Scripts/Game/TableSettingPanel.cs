using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ClubPoker.Game;
using UnityEngine.UI;

public class TableSettingPanel : MonoBehaviour
{
    public static TableSettingPanel Instance { get; private set; }

    public Button CloseBotton;
    public GameObject TableSettingScreen;
    public Sprite OFFButton_sprite;
    public Sprite ONButton_sprite;

    public Color OnButton_Color;
    public Color OFFButton_Color;

    public Button EmojiButton;
    public Button Chips_In_BB_Button;
    public Button RunIT_multitimes_Button;
    public Button Sound_Button;
    public Button VoiceMessage_Button;
    public Button TextMessage_Button;
    public Button Vibration_Button;
    public Button Squeeze_Button;
    public Button AccurateBetting_Button;
    public Button CustomizeActionButton;

    public Text EmojiText;
    public Text Chips_In_BB_Text;
    public Text RunIT_multitimes_Text;
    public Text Sound_Text;
    public Text VoiceMessage_Text;
    public Text TextMessage_Text;
    public Text Vibration_Text;
    public Text Squeeze_Text;
    public Text AccurateBetting_Text;
    public Text CustomizeActionText;

    public GameObject CustomizeActionPanel;

    public Button Pot_1_2_Button;
    public Button Pot_2_3_Button;
    public Button Pot_Button;
    public Text Pot_1_2_Text;
    public Text Pot_2_3_Text;
    public Text Pot_Text;
    public Slider BetSlider;

    public Button Raise_2x_Button;
    public Button Raise_3x_Button;
    public Button Raise_4x_Button;
    public Text Raise_2x_Text;
    public Text Raise_3x_Text;
    public Text Raise_4x_Text;
    public Slider Raise_Slider;

    public Sprite SelectButton_Sprite;
    public Sprite UnSelectButton_Sprite;

    private bool emojiOn;
    private bool chipsInBBOn;
    private bool runITMultitimesOn;
    private bool soundOn;
    private bool voiceMessageOn;
    private bool textMessageOn;
    private bool vibrationOn;
    private bool squeezeOn;
    private bool accurateBettingOn;
    private bool customizeActionOn;


    private readonly string[] potOptions = { "1/5", "1/4", "1/3", "2/5", "1/2", "3/5", "2/3", "3/4", "4/5", "Pot", "1.5 Pot", "2 Pot" };
    private readonly string[] raiseOptions = { "2x", "2.1x", "2.2x", "2.3x", "2.4x", "2.5x", "2.6x", "2.7x", "2.8x", "2.9x", "3x", "3.5x", "4x", "4.5x", "5x", "5.5x", "6x" };

    private readonly int[] potValueIndexes = { 4, 6, 9 };
    private readonly int[] raiseValueIndexes = { 0, 10, 12 };

    private int selectedPotButton;
    private int selectedRaiseButton;


    private void Awake()
    {
        Instance = this;
    }
    private void OnEnable()
    {
        ChipDisplayFormatter.Changed += RefreshChipsInBBButton;
        RefreshChipsInBBButton();
    }

    private void OnDisable()
    {
        ChipDisplayFormatter.Changed -= RefreshChipsInBBButton;
    }

    private void RefreshChipsInBBButton()
    {
        chipsInBBOn = ChipDisplayFormatter.ShowInBB;
        UpdateButtonVisual(Chips_In_BB_Button, Chips_In_BB_Text, chipsInBBOn);
    }

    private void Start()
    {
        if (EmojiButton != null) EmojiButton.onClick.AddListener(EmojiButtonOnClick);
        if (Chips_In_BB_Button != null) Chips_In_BB_Button.onClick.AddListener(ChipsInBBButtonOnClick);
        if (RunIT_multitimes_Button != null) RunIT_multitimes_Button.onClick.AddListener(RunITMultitimesButtonOnClick);
        if (Sound_Button != null) Sound_Button.onClick.AddListener(SoundButtonOnClick);
        if (VoiceMessage_Button != null) VoiceMessage_Button.onClick.AddListener(VoiceMessageButtonOnClick);
        if (TextMessage_Button != null) TextMessage_Button.onClick.AddListener(TextMessageButtonOnClick);
        if (Vibration_Button != null) Vibration_Button.onClick.AddListener(VibrationButtonOnClick);
        if (Squeeze_Button != null) Squeeze_Button.onClick.AddListener(SqueezeButtonOnClick);
        if (AccurateBetting_Button != null) AccurateBetting_Button.onClick.AddListener(AccurateBettingButtonOnClick);
        if (CustomizeActionButton != null) CustomizeActionButton.onClick.AddListener(CustomizeActionButtonOnClick);
        CloseBotton.onClick.AddListener(CloseBottonOnTap);
        RefreshAllButtons();
        SetupCustomizeSliders();
        if (CustomizeActionPanel != null)
            CustomizeActionPanel.SetActive(customizeActionOn);
    }


    void CloseBottonOnTap()
    {
        TableSettingScreen.SetActive(false);
    }
    private void SetupCustomizeSliders()
    {
        if (Pot_1_2_Button != null) Pot_1_2_Button.onClick.AddListener(PotHalfButtonOnClick);
        if (Pot_2_3_Button != null) Pot_2_3_Button.onClick.AddListener(PotTwoThirdButtonOnClick);
        if (Pot_Button != null) Pot_Button.onClick.AddListener(PotButtonOnClick);

        if (Raise_2x_Button != null) Raise_2x_Button.onClick.AddListener(Raise2xButtonOnClick);
        if (Raise_3x_Button != null) Raise_3x_Button.onClick.AddListener(Raise3xButtonOnClick);
        if (Raise_4x_Button != null) Raise_4x_Button.onClick.AddListener(Raise4xButtonOnClick);

        if (BetSlider != null)
        {
            BetSlider.minValue = 0;
            BetSlider.maxValue = potOptions.Length - 1;
            BetSlider.wholeNumbers = true;
            BetSlider.onValueChanged.AddListener(BetSliderOnValueChanged);
        }

        if (Raise_Slider != null)
        {
            Raise_Slider.minValue = 0;
            Raise_Slider.maxValue = raiseOptions.Length - 1;
            Raise_Slider.wholeNumbers = true;
            Raise_Slider.onValueChanged.AddListener(RaiseSliderOnValueChanged);
        }

        RefreshPotTexts();
        RefreshRaiseTexts();

        SelectPotButton(0);
        SelectRaiseButton(0);
    }

    public void PotHalfButtonOnClick()
    {
        SelectPotButton(0);
    }

    public void PotTwoThirdButtonOnClick()
    {
        SelectPotButton(1);
    }

    public void PotButtonOnClick()
    {
        SelectPotButton(2);
    }

    public void Raise2xButtonOnClick()
    {
        SelectRaiseButton(0);
    }

    public void Raise3xButtonOnClick()
    {
        SelectRaiseButton(1);
    }

    public void Raise4xButtonOnClick()
    {
        SelectRaiseButton(2);
    }

    private void SelectPotButton(int index)
    {
        selectedPotButton = index;

        SetPresetButtonSprite(Pot_1_2_Button, index == 0);
        SetPresetButtonSprite(Pot_2_3_Button, index == 1);
        SetPresetButtonSprite(Pot_Button, index == 2);

        if (BetSlider != null)
            BetSlider.SetValueWithoutNotify(potValueIndexes[index]);
    }

    private void SelectRaiseButton(int index)
    {
        selectedRaiseButton = index;

        SetPresetButtonSprite(Raise_2x_Button, index == 0);
        SetPresetButtonSprite(Raise_3x_Button, index == 1);
        SetPresetButtonSprite(Raise_4x_Button, index == 2);

        if (Raise_Slider != null)
            Raise_Slider.SetValueWithoutNotify(raiseValueIndexes[index]);
    }

    public void BetSliderOnValueChanged(float value)
    {
        int index = Mathf.Clamp(
            Mathf.RoundToInt(value), 0, potOptions.Length - 1);

        potValueIndexes[selectedPotButton] = index;
        RefreshPotTexts();
    }

    public void RaiseSliderOnValueChanged(float value)
    {
        int index = Mathf.Clamp(
            Mathf.RoundToInt(value), 0, raiseOptions.Length - 1);

        raiseValueIndexes[selectedRaiseButton] = index;
        RefreshRaiseTexts();
    }

    private void RefreshPotTexts()
    {
        if (Pot_1_2_Text != null) Pot_1_2_Text.text = potOptions[potValueIndexes[0]];
        if (Pot_2_3_Text != null) Pot_2_3_Text.text = potOptions[potValueIndexes[1]];
        if (Pot_Text != null) Pot_Text.text = potOptions[potValueIndexes[2]];
    }

    private void RefreshRaiseTexts()
    {
        if (Raise_2x_Text != null) Raise_2x_Text.text = raiseOptions[raiseValueIndexes[0]];
        if (Raise_3x_Text != null) Raise_3x_Text.text = raiseOptions[raiseValueIndexes[1]];
        if (Raise_4x_Text != null) Raise_4x_Text.text = raiseOptions[raiseValueIndexes[2]];
    }

    private void SetPresetButtonSprite(Button button, bool selected)
    {
        if (button == null || button.image == null) return;

        Sprite sprite = selected ? SelectButton_Sprite : UnSelectButton_Sprite;
        if (sprite == null) return;

        button.image.sprite = sprite;

        if (button.transition == Selectable.Transition.SpriteSwap)
        {
            SpriteState state = button.spriteState;
            state.highlightedSprite = sprite;
            state.pressedSprite = sprite;
            state.selectedSprite = sprite;
            state.disabledSprite = sprite;
            button.spriteState = state;
        }
    }

    private void RemoveCustomizeSliderListeners()
    {
        if (Pot_1_2_Button != null) Pot_1_2_Button.onClick.RemoveListener(PotHalfButtonOnClick);
        if (Pot_2_3_Button != null) Pot_2_3_Button.onClick.RemoveListener(PotTwoThirdButtonOnClick);
        if (Pot_Button != null) Pot_Button.onClick.RemoveListener(PotButtonOnClick);

        if (Raise_2x_Button != null) Raise_2x_Button.onClick.RemoveListener(Raise2xButtonOnClick);
        if (Raise_3x_Button != null) Raise_3x_Button.onClick.RemoveListener(Raise3xButtonOnClick);
        if (Raise_4x_Button != null) Raise_4x_Button.onClick.RemoveListener(Raise4xButtonOnClick);

        if (BetSlider != null)
            BetSlider.onValueChanged.RemoveListener(BetSliderOnValueChanged);

        if (Raise_Slider != null)
            Raise_Slider.onValueChanged.RemoveListener(RaiseSliderOnValueChanged);
    }
    public void EmojiButtonOnClick()
    {
        emojiOn = !emojiOn;
        UpdateButtonVisual(EmojiButton, EmojiText, emojiOn);
    }

    public void ChipsInBBButtonOnClick()
    {
        ChipDisplayFormatter.SetShowInBB(!ChipDisplayFormatter.ShowInBB);
        RefreshChipsInBBButton();
    }

    public void RunITMultitimesButtonOnClick()
    {
        runITMultitimesOn = !runITMultitimesOn;
        UpdateButtonVisual(RunIT_multitimes_Button, RunIT_multitimes_Text, runITMultitimesOn);
    }

    public void SoundButtonOnClick()
    {
        soundOn = !soundOn;
        UpdateButtonVisual(Sound_Button, Sound_Text, soundOn);
    }

    public void VoiceMessageButtonOnClick()
    {
        voiceMessageOn = !voiceMessageOn;
        UpdateButtonVisual(VoiceMessage_Button, VoiceMessage_Text, voiceMessageOn);
    }

    public void TextMessageButtonOnClick()
    {
        textMessageOn = !textMessageOn;
        UpdateButtonVisual(TextMessage_Button, TextMessage_Text, textMessageOn);
    }

    public void VibrationButtonOnClick()
    {
        vibrationOn = !vibrationOn;
        UpdateButtonVisual(Vibration_Button, Vibration_Text, vibrationOn);
    }

    public void SqueezeButtonOnClick()
    {
        squeezeOn = !squeezeOn;
        UpdateButtonVisual(Squeeze_Button, Squeeze_Text, squeezeOn);
    }

    public void AccurateBettingButtonOnClick()
    {
        accurateBettingOn = !accurateBettingOn;
        UpdateButtonVisual(AccurateBetting_Button, AccurateBetting_Text, accurateBettingOn);
    }

    public void CustomizeActionButtonOnClick()
    {
        customizeActionOn = !customizeActionOn;
        UpdateButtonVisual(CustomizeActionButton, CustomizeActionText, customizeActionOn);

        if (CustomizeActionPanel != null)
            CustomizeActionPanel.SetActive(customizeActionOn);
    }

    private void RefreshAllButtons()
    {
        UpdateButtonVisual(EmojiButton, EmojiText, emojiOn);
        UpdateButtonVisual(Chips_In_BB_Button, Chips_In_BB_Text, chipsInBBOn);
        UpdateButtonVisual(RunIT_multitimes_Button, RunIT_multitimes_Text, runITMultitimesOn);
        UpdateButtonVisual(Sound_Button, Sound_Text, soundOn);
        UpdateButtonVisual(VoiceMessage_Button, VoiceMessage_Text, voiceMessageOn);
        UpdateButtonVisual(TextMessage_Button, TextMessage_Text, textMessageOn);
        UpdateButtonVisual(Vibration_Button, Vibration_Text, vibrationOn);
        UpdateButtonVisual(Squeeze_Button, Squeeze_Text, squeezeOn);
        UpdateButtonVisual(AccurateBetting_Button, AccurateBetting_Text, accurateBettingOn);
        UpdateButtonVisual(CustomizeActionButton, CustomizeActionText, customizeActionOn);
    }

    private void UpdateButtonVisual(Button button, Text buttonText, bool isOn)
    {
        Sprite sprite = isOn ? ONButton_sprite : OFFButton_sprite;

        if (button != null && button.image != null && sprite != null)
        {
            button.image.sprite = sprite;

            if (button.transition == Selectable.Transition.SpriteSwap)
            {
                SpriteState state = button.spriteState;
                state.highlightedSprite = sprite;
                state.pressedSprite = sprite;
                state.selectedSprite = sprite;
                state.disabledSprite = sprite;
                button.spriteState = state;
            }
        }

        if (buttonText != null)
            buttonText.color = isOn ? OnButton_Color : OFFButton_Color;
    }

    public bool IsButtonOn(Button button)
    {
        if (button == null) return false;
        if (button == EmojiButton) return emojiOn;
        if (button == Chips_In_BB_Button) return chipsInBBOn;
        if (button == RunIT_multitimes_Button) return runITMultitimesOn;
        if (button == Sound_Button) return soundOn;
        if (button == VoiceMessage_Button) return voiceMessageOn;
        if (button == TextMessage_Button) return textMessageOn;
        if (button == Vibration_Button) return vibrationOn;
        if (button == Squeeze_Button) return squeezeOn;
        if (button == AccurateBetting_Button) return accurateBettingOn;
        if (button == CustomizeActionButton) return customizeActionOn;
        return false;
    }

    private void OnDestroy()
    {
        ChipDisplayFormatter.Changed -= RefreshChipsInBBButton;
        if (Instance == this) Instance = null;
        if (CloseBotton != null) CloseBotton.onClick.RemoveListener(CloseBottonOnTap);
        if (EmojiButton != null) EmojiButton.onClick.RemoveListener(EmojiButtonOnClick);
        if (Chips_In_BB_Button != null) Chips_In_BB_Button.onClick.RemoveListener(ChipsInBBButtonOnClick);
        if (RunIT_multitimes_Button != null) RunIT_multitimes_Button.onClick.RemoveListener(RunITMultitimesButtonOnClick);
        if (Sound_Button != null) Sound_Button.onClick.RemoveListener(SoundButtonOnClick);
        if (VoiceMessage_Button != null) VoiceMessage_Button.onClick.RemoveListener(VoiceMessageButtonOnClick);
        if (TextMessage_Button != null) TextMessage_Button.onClick.RemoveListener(TextMessageButtonOnClick);
        if (Vibration_Button != null) Vibration_Button.onClick.RemoveListener(VibrationButtonOnClick);
        if (Squeeze_Button != null) Squeeze_Button.onClick.RemoveListener(SqueezeButtonOnClick);
        if (AccurateBetting_Button != null) AccurateBetting_Button.onClick.RemoveListener(AccurateBettingButtonOnClick);
        if (CustomizeActionButton != null) CustomizeActionButton.onClick.RemoveListener(CustomizeActionButtonOnClick);

        RemoveCustomizeSliderListeners();
    }
}