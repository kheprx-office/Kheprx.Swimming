export interface SwimmerChampionshipRace {
  dayLabelEn: string;
  dayLabelAr: string | null;
  distanceId: string;
  strokeId: string;
  timeMs: number;
  isPersonalBest: boolean;
}

export interface SwimmerChampionshipHistory {
  eventId: string;
  nameEn: string;
  nameAr: string | null;
  startDate: string; // 'YYYY-MM-DD'
  endDate: string;   // 'YYYY-MM-DD'
  locationEn: string;
  locationAr: string | null;
  races: SwimmerChampionshipRace[];
}

interface NameLookup { id: string; nameEn: string; nameAr: string | null; }

/** "<distance> <stroke>" in the active language; a missing lookup contributes nothing (no crash). */
export function resolveRaceName(
  distances: NameLookup[], strokes: NameLookup[], distanceId: string, strokeId: string, lang: string): string {
  const label = (items: NameLookup[], id: string): string => {
    const item = items.find((i) => i.id === id);
    if (!item) return '';
    return lang === 'ar' ? (item.nameAr ?? item.nameEn) : item.nameEn;
  };
  return [label(distances, distanceId), label(strokes, strokeId)].filter(Boolean).join(' ');
}
