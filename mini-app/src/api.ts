const API_URL = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')

function getInitData(): string {
  return window.Telegram?.WebApp?.initData ?? ''
}

export async function apiPost<T>(path: string, body: T): Promise<void> {
  const res = await fetch(`${API_URL}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `tma ${getInitData()}`,
    },
    body: JSON.stringify(body),
  })

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new Error(text || `HTTP ${res.status}`)
  }
}
