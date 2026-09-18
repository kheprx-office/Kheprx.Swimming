export interface CreatedSwimmer {
  id: string;
  uid: string;
  username: string;
  nameEn: string;
  temporaryPassword: string;
}

export interface SwimmerListItem {
  id: string;
  uid: string;
  nameEn: string;
  nameAr: string | null;
  clubNameEn: string | null;
  clubNameAr: string | null;
  gender: 'male' | 'female' | null;
  age: number | null;
}
