using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System;

public class GameStartController : MonoBehaviour
{
    [SerializeField, Min(1)] private int bagSlotCount = 16;
    [SerializeField, Min(1)] private int chestSlotCount = 64;

    [Header("UI 참조")]
    [SerializeField] private GameObject continueButton;
    [SerializeField] private RectTransform warningPanel;


    private bool isStarting;

    private void Awake()
    {
        RefreshContinueButtonVisibility();
        if (warningPanel != null)
            warningPanel.gameObject.SetActive(false);
    }

    public void OnNewGameClicked()
    {
        StartNewGameAsync().Forget();
    }

    public void OnContinueClicked()
    {
        if (isStarting || GameSceneManager.IsLoading)
            return;

        GameSaveService service = GameSaveContext.Service;

        if (service == null)
            return;

        if (!service.HasSaveData)
            return;

        isStarting = true;

        try
        {
            service.Load();

            GameSceneManager.MoveToNextDestination(GameScene.Lobby);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to continue the game: {ex.Message}");
        }
        finally
        {
            isStarting = false;
        }
    }

    public void OnExitClicked()
    {
        Application.Quit();
    }

    public void OnCancelClicked()
    {
        warningPanel.gameObject.SetActive(false);
    }

    public void OnConfirmClicked()
    {
        ConfirmStartNewGameAsync().Forget();
    }

    private void RefreshContinueButtonVisibility()
    {
        // 버튼의 SetActive 상태를 저장된 게임 데이터의 존재 여부에 따라 설정
        continueButton.SetActive(GameSaveContext.Service != null && GameSaveContext.Service.HasSaveData);
    }

    private async UniTask StartNewGameAsync()
    {
        if (isStarting || GameSceneManager.IsLoading)
            return;

        GameSaveService service = GameSaveContext.Service;

        if (service == null)
            return;

        if (service.HasSaveData)
        {
            if (warningPanel != null)
            {
                warningPanel.gameObject.SetActive(true);
                return;
            }
        }

        isStarting = true;

        try
        {
            GameSaveData newData = GameSaveDataFactory.CreateNewGameData(bagSlotCount, chestSlotCount);

            if (newData == null)
                return;

            await service.SaveAsync(newData);
            await service.FlushAsync();
            GameSceneManager.MoveToNextDestination(GameScene.Lobby);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to start a new game: {ex.Message}");
        }
        finally
        {
            isStarting = false;
        }
    }

    private async UniTask ConfirmStartNewGameAsync()
    {
        if (isStarting || GameSceneManager.IsLoading)
            return;
        GameSaveService service = GameSaveContext.Service;
        if (service == null)
            return;
        isStarting = true;
        try
        {
            GameSaveData newData = GameSaveDataFactory.CreateNewGameData(bagSlotCount, chestSlotCount);

            if (newData == null)
                return;

            await service.SaveAsync(newData);
            await service.FlushAsync();
            GameSceneManager.MoveToNextDestination(GameScene.Lobby);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to confirm starting a new game: {ex.Message}");
        }
        finally
        {
            isStarting = false;
        }
    }
}
