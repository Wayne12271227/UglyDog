using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class ButtonHoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Color hoverTint = new Color(1f, 0.92f, 0.72f, 1f);
    [SerializeField, Range(0f, 1f)] private float hoverStrength = 0.18f;

    private Selectable selectable;
    private Color normalColor;
    private bool isPointerOver;
    private bool isSelected;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();

        if (targetGraphic == null)
        {
            targetGraphic = selectable.targetGraphic != null
                ? selectable.targetGraphic
                : GetComponent<Graphic>();
        }

        if (targetGraphic != null)
        {
            normalColor = targetGraphic.color;
        }
    }

    private void OnEnable()
    {
        if (targetGraphic != null)
        {
            normalColor = targetGraphic.color;
            ApplyTint();
        }
    }

    private void OnDisable()
    {
        isPointerOver = false;
        isSelected = false;

        if (targetGraphic != null)
        {
            targetGraphic.color = normalColor;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        ApplyTint();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        ApplyTint();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        ApplyTint();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerOver = RectTransformUtility.RectangleContainsScreenPoint(
            transform as RectTransform,
            eventData.position,
            eventData.enterEventCamera);

        ApplyTint();
    }

    public void OnSelect(BaseEventData eventData)
    {
        isSelected = true;
        ApplyTint();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        isSelected = false;
        ApplyTint();
    }

    private void ApplyTint()
    {
        if (targetGraphic == null)
        {
            return;
        }

        bool shouldHoverTint = selectable == null || selectable.interactable
            ? isPointerOver || isSelected
            : false;

        targetGraphic.color = shouldHoverTint
            ? Color.Lerp(normalColor, WithAlpha(hoverTint, normalColor.a), hoverStrength)
            : normalColor;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
