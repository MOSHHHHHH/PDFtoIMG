/**
 * Typewriter effect for the hero headline.
 * Cycles through a fixed list of Hebrew sentences: types each one out,
 * pauses, deletes it, then moves to the next - looping forever.
 * Respects prefers-reduced-motion by just swapping text with a fade.
 */
(function () {
  const SENTENCES = [
    "אתה צריך חידושי הרמב\"ן על יבמות בטלפון שלך",
    "אתה צריך אקורדים לשיר 'נפשי' בטלפון שלך",
    "אתה צריך רשימת ועד קמ\"ב בטלפון שלך"
  ];

  const TYPE_SPEED_MS = 45;
  const DELETE_SPEED_MS = 25;
  const HOLD_MS = 1800;
  const GAP_MS = 400;

  const target = document.getElementById("typewriter-text");
  if (!target) return;

  const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

  if (prefersReducedMotion) {
    let index = 0;
    target.textContent = SENTENCES[0];
    setInterval(() => {
      index = (index + 1) % SENTENCES.length;
      target.style.transition = "opacity 0.3s ease";
      target.style.opacity = "0";
      setTimeout(() => {
        target.textContent = SENTENCES[index];
        target.style.opacity = "1";
      }, 300);
    }, HOLD_MS + 600);
    return;
  }

  let sentenceIndex = 0;
  let charIndex = 0;
  let deleting = false;

  function tick() {
    const current = SENTENCES[sentenceIndex];

    if (!deleting) {
      charIndex++;
      target.textContent = current.slice(0, charIndex);

      if (charIndex === current.length) {
        deleting = true;
        setTimeout(tick, HOLD_MS);
        return;
      }
      setTimeout(tick, TYPE_SPEED_MS);
    } else {
      charIndex--;
      target.textContent = current.slice(0, charIndex);

      if (charIndex === 0) {
        deleting = false;
        sentenceIndex = (sentenceIndex + 1) % SENTENCES.length;
        setTimeout(tick, GAP_MS);
        return;
      }
      setTimeout(tick, DELETE_SPEED_MS);
    }
  }

  tick();
})();
