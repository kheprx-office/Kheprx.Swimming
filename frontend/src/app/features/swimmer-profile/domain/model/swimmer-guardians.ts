export interface Guardian {
  id: string;
  relationCode: string; // 'father' | 'mother'
  name: string;
  nationalId: string;
  phone: string;
}

export interface SwimmerGuardians {
  father: Guardian | null;
  mother: Guardian | null;
}
