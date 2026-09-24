import { SwimmerChampionshipHistoryData } from '@features/championships/data/dto/swimmer-championship-history.dto';
import { SwimmerChampionshipHistory } from '@features/championships/domain/model/swimmer-championship-history';

export function toSwimmerChampionshipHistory(data: SwimmerChampionshipHistoryData[]): SwimmerChampionshipHistory[] {
  return data.map((c) => ({
    eventId: c.eventId,
    nameEn: c.nameEn,
    nameAr: c.nameAr,
    startDate: c.startDate,
    endDate: c.endDate,
    locationEn: c.locationEn,
    locationAr: c.locationAr,
    races: c.races.map((r) => ({
      dayLabelEn: r.dayLabelEn,
      dayLabelAr: r.dayLabelAr,
      distanceId: r.distanceId,
      strokeId: r.strokeId,
      timeMs: r.timeMs,
      isPersonalBest: r.isPersonalBest,
    })),
  }));
}
