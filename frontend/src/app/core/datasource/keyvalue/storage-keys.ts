// StorageKeys: the single registry of localStorage keys used across the app.
// New features add their keys here.
export const StorageKeys = {
  profile: '@offline-storage/profile',
  accessToken: '@auth/access-token',
  refreshToken: '@auth/refresh-token',
} as const;
