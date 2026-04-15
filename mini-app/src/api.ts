const API_URL = (import.meta.env.VITE_API_URL ?? '').replace(/\/$/, '')

function getAuthHeader(): string | null {
  const params = new URLSearchParams(window.location.search)
  const token = params.get('token')
  if (token) return `token ${token}`

  const initData = window.Telegram?.WebApp?.initData ?? ''
  if (initData) return `tma ${initData}`

  return null
}

export async function apiPost<T>(path: string, body: T): Promise<void> {
  const auth = getAuthHeader()
  if (!auth) {
    throw new Error('Открой приложение через кнопку в Telegram')
  }

  const res = await fetch(`${API_URL}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': auth,
    },
    body: JSON.stringify(body),
  })

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText)
    throw new Error(text || `HTTP ${res.status}`)
  }
}
