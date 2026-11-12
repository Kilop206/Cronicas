export function safeReturnUrl(requested: string | null): string {
  if (!requested?.startsWith('/') || requested.startsWith('//') || /[\\\u0000-\u001f]/.test(requested)) return '/dashboard';
  // Returning to a guest-only route after login would repeatedly trigger its redirect guard.
  if (/^\/(?:login|register|forgot-password|reset-password)(?:[/?#;]|$)/i.test(requested)) return '/dashboard';
  return requested;
}
