using System.Collections;
using Richman.Core;
using UnityEngine;

namespace Richman.Presentation
{
    public sealed class DiceView : MonoBehaviour
    {
        [SerializeField] private Transform diceVisual;
        [SerializeField] private TextMesh resultLabel;

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;

        public void SetPrefabReferences(Transform visual, TextMesh label)
        {
            diceVisual = visual;
            resultLabel = label;
            _baseLocalPosition = diceVisual == null ? Vector3.zero : diceVisual.localPosition;
            _baseLocalRotation = diceVisual == null ? Quaternion.identity : diceVisual.localRotation;
        }

        public IEnumerator PlayRoll(DiceResult result)
        {
            var visual = diceVisual == null ? transform : diceVisual;
            var startPosition = visual.localPosition;
            var startRotation = visual.localRotation;
            var elapsed = 0f;
            const float duration = 1.15f;
            if (resultLabel != null) resultLabel.text = "";

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                visual.localPosition = startPosition + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.9f;
                visual.localRotation = startRotation * Quaternion.Euler(
                    540f * eased,
                    720f * eased,
                    360f * eased);
                yield return null;
            }

            visual.localPosition = _baseLocalPosition;
            visual.localRotation = _baseLocalRotation;
            if (resultLabel != null) resultLabel.text = "" + result.Total;
        }
    }
}
