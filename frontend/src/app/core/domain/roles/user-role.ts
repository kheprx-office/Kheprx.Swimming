// UserRole is the swimming role set. Head coach has broader access than captain.
// Shared kernel: used by auth, users, and layout.
export type UserRole = 'head_coach' | 'captain';
