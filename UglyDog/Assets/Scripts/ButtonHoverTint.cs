using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class ButtonHoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Color hoverTint = new Color(1f, 0.86f, 0.42f, 1f);
    [SerializeField, Range(0f, 1f)] private float hoverStrength = 0.48f;
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressedScale = 0.97f;
    [SerializeField] private float transitionSpeed = 14f;

    private Selectable selectable;
    private Color normalColor;
    private Vector3 normalScale;
    private Color targetColor;
    private Vector3 targetScale;
    private bool isPointerOver;
    private bool isSelected;
    private bool isPointerDown;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
        normalScale = transform.localScale;
        targetScale = normalScale;

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

        targetColor = normalColor;
    }

    private void OnEnable()
    {
        normalScale = transform.localScale;
        targetScale = normalScale;
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
        isPointerDown = false;

        if (targetGraphic != null)
        {
            targetGraphic.color = normalColor;
        }

        transform.localScale = normalScale;
    }

    private void Update()
    {
        float t = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        if (targetGraphic != null)
        {
            targetGraphic.color = Color.Lerp(targetGraphic.color, targetColor, t);
        }

        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);
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
        isPointerDown = true;
        ApplyTint();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
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

        targetColor = shouldHoverTint
            ? Color.Lerp(normalColor, WithAlpha(hoverTint, normalColor.a), hoverStrength)
            : normalColor;
        targetScale = normalScale * (isPointerDown ? pressedScale : shouldHoverTint ? hoverScale : 1f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
