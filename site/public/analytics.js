// GA4 with Consent Mode v2, loaded from a file rather than an inline block.
//
// This site's Content-Security-Policy has no 'unsafe-inline' in script-src and
// should not gain one. Google's own snippet is inline, so it is rewritten here
// as an ordinary same-origin module and the tag itself is injected. Nothing
// about the measurement changes; only where the code lives.
//
// DENIED BY DEFAULT. Nothing is loaded and no cookie is set until the visitor
// accepts. consent.js owns the prompt and calls __dbhqEnableGA() on accept, and
// __dbhqRevokeGA() when the reader withdraws. That ordering is not a nicety: GA4
// sets its cookie on the shared .dbhq.uk parent, so a cookie dropped by this
// site without consent would be a cookie across every site under that domain.
//
// THE MEASUREMENT ID IS SHARED ACROSS THE DBHQ SITES ON PURPOSE. The analytics
// property carries exactly one data stream. A stream of this site's own would
// give this host its own cookie on that same shared parent, so a reader arriving
// from dbhq.uk would start a fresh session here and the journey between the two
// would be lost. It would also strand this site outside the Search Console
// reporting, which binds to one data stream. Split the sites at reporting time
// with the Hostname dimension instead. Do not create a data stream for this site.
const MEASUREMENT_ID = "G-3H3NFGSX85";

// Never measure a local preview or a Pages branch build - only the real host.
// 6502.pages.dev serves the same bytes and is not the site.
const PROD = location.hostname === "6502.dbhq.uk";

window.dataLayer = window.dataLayer || [];
function gtag() {
  // Deliberately `arguments`, not a rest parameter: gtag reads the live
  // Arguments object itself, and a real array does not behave the same way.
  window.dataLayer.push(arguments);
}
window.gtag = gtag;

gtag("consent", "default", {
  ad_storage: "denied",
  analytics_storage: "denied",
  ad_user_data: "denied",
  ad_personalization: "denied",
});

window.__dbhqEnableGA = function () {
  // Accepting again after a withdrawal, in the same page load: the tag is
  // already there, so only the consent signal needs to go back to granted.
  if (PROD && window.__gaLoaded) gtag("consent", "update", { analytics_storage: "granted" });
  if (!PROD || window.__gaLoaded) return;
  window.__gaLoaded = true;

  gtag("consent", "update", { analytics_storage: "granted" });
  gtag("js", new Date());
  gtag("config", MEASUREMENT_ID);

  const tag = document.createElement("script");
  tag.async = true;
  tag.src = "https://www.googletagmanager.com/gtag/js?id=" + MEASUREMENT_ID;
  document.head.appendChild(tag);
};

// Withdrawing consent puts all four signals back to denied. GA4 then stops
// measuring and stops writing its cookie. It does not delete a cookie already set.
window.__dbhqRevokeGA = function () {
  gtag("consent", "update", {
    ad_storage: "denied",
    analytics_storage: "denied",
    ad_user_data: "denied",
    ad_personalization: "denied",
  });
};

// A visitor who accepted on a previous visit is not asked again. The key is
// shared across *.dbhq.uk, so accepting on dbhq.uk carries over to here and
// the reader is asked once across the estate rather than once per site.
try {
  if (localStorage.getItem("dbhq-consent") === "granted") window.__dbhqEnableGA();
} catch (e) {
  // localStorage throws rather than returning null in some privacy modes.
  // No stored choice means no consent, which is already the default.
}
