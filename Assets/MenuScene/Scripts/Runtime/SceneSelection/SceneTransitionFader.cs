using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VRStrokeRehab.MenuScene
{
    public class SceneTransitionFader : MonoBehaviour
    {
        private const string RuntimeObjectName = "SceneTransitionFader";
        private const string FadeObjectName = "OVRSceneFade";

        [SerializeField] private float fadeDuration = 0.35f;
        [SerializeField] private Color fadeColor = Color.black;

        private static SceneTransitionFader instance;

        private OVRScreenFade ovrScreenFade;
        private Coroutine transitionRoutine;
        private Camera attachedCamera;

        public static bool IsTransitioning
        {
            get { return instance != null && instance.transitionRoutine != null; }
        }

        public static SceneTransitionFader Instance
        {
            get
            {
                if (instance == null)
                {
                    CreateRuntimeInstance();
                }

                return instance;
            }
        }

        public static bool TryLoadScene(MonoBehaviour host, string sceneReference)
        {
            if (host == null || string.IsNullOrWhiteSpace(sceneReference))
            {
                return false;
            }

            return Instance.BeginSceneLoad(host, sceneReference);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureFadeComponent();
        }

        private void LateUpdate()
        {
            AttachToActiveCamera();
        }

        private bool BeginSceneLoad(MonoBehaviour host, string sceneReference)
        {
            if (transitionRoutine != null)
            {
                return false;
            }

            transitionRoutine = StartCoroutine(LoadSceneRoutine(sceneReference));
            return true;
        }

        private IEnumerator LoadSceneRoutine(string sceneReference)
        {
            EnsureFadeComponent();
            AttachToActiveCamera(true);
            yield return null;

            ovrScreenFade.fadeTime = fadeDuration;
            ovrScreenFade.fadeColor = fadeColor;
            ovrScreenFade.fadeOnStart = false;
            ovrScreenFade.SetExplicitFade(0f);
            ovrScreenFade.FadeOut();

            yield return new WaitForSecondsRealtime(fadeDuration);

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneReference);
            if (loadOperation == null)
            {
                transitionRoutine = null;
                yield break;
            }

            while (!loadOperation.isDone)
            {
                yield return null;
            }

            yield return null;
            AttachToActiveCamera(true);

            ovrScreenFade.fadeTime = fadeDuration;
            ovrScreenFade.FadeIn();

            yield return new WaitForSecondsRealtime(fadeDuration);
            transitionRoutine = null;
        }

        private void EnsureFadeComponent()
        {
            if (ovrScreenFade != null)
            {
                return;
            }

            var fadeTransform = transform.Find(FadeObjectName);
            GameObject fadeObject;
            if (fadeTransform == null)
            {
                fadeObject = new GameObject(FadeObjectName);
                fadeObject.transform.SetParent(transform, false);
            }
            else
            {
                fadeObject = fadeTransform.gameObject;
            }

            ovrScreenFade = fadeObject.GetComponent<OVRScreenFade>();
            if (ovrScreenFade == null)
            {
                ovrScreenFade = fadeObject.AddComponent<OVRScreenFade>();
            }

            ovrScreenFade.fadeOnStart = false;
            ovrScreenFade.fadeTime = fadeDuration;
            ovrScreenFade.fadeColor = fadeColor;
            ovrScreenFade.SetExplicitFade(0f);
        }

        private void AttachToActiveCamera(bool force = false)
        {
            var currentCamera = Camera.main;
            if (currentCamera == null)
            {
                return;
            }

            if (!force && attachedCamera == currentCamera)
            {
                return;
            }

            attachedCamera = currentCamera;
            transform.SetParent(currentCamera.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static void CreateRuntimeInstance()
        {
            var runtimeObject = new GameObject(RuntimeObjectName);
            runtimeObject.AddComponent<SceneTransitionFader>();
        }
    }
}
