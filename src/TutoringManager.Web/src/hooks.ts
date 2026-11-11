import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'

export interface Loaded<T> {
  data: T | undefined
  error: ApiError | undefined
  loading: boolean
  reload: () => void
}

/** Loads a GET endpoint and exposes the four states every data region shows. */
export function useLoad<T>(url: string | null): Loaded<T> {
  const [data, setData] = useState<T>()
  const [error, setError] = useState<ApiError>()
  const [loading, setLoading] = useState(url !== null)
  const [version, setVersion] = useState(0)

  useEffect(() => {
    if (url === null) return
    let current = true
    setLoading(true)
    setError(undefined)
    api.get<T>(url)
      .then((result) => { if (current) setData(result) })
      .catch((e: ApiError) => { if (current) setError(e) })
      .finally(() => { if (current) setLoading(false) })
    return () => { current = false }
  }, [url, version])

  const reload = useCallback(() => setVersion((v) => v + 1), [])
  return { data, error, loading, reload }
}
