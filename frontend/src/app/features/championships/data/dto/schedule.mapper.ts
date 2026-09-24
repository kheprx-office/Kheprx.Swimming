import { ScheduleDtoData, SetScheduleRq } from '@features/championships/data/dto/schedule.dto';
import { ScheduleDayData } from '@features/championships/domain/model/competition-schedule';
import { RaceScheduleDay } from '@features/championships/domain/model/race-result';

/** DTO tree → key-less data the ViewModel hydrates with client keys. Time normalized to 'HH:mm'. */
export function toScheduleDataList(dto: ScheduleDtoData): ScheduleDayData[] {
  return dto.days.map((day) => ({
    labelEn: day.labelEn,
    labelAr: day.labelAr,
    dayDate: day.dayDate,
    races: day.races.map((r) => ({
      strokeId: r.strokeId,
      distanceId: r.distanceId,
      scheduledTime: r.scheduledTime ? r.scheduledTime.slice(0, 5) : null,
      swimmerIds: [...r.swimmerIds],
    })),
  }));
}

/** Key-less data → PUT body. */
export function toSetScheduleRq(days: ScheduleDayData[]): SetScheduleRq {
  return {
    days: days.map((d) => ({
      labelEn: d.labelEn,
      labelAr: d.labelAr,
      dayDate: d.dayDate,
      races: d.races.map((r) => ({
        strokeId: r.strokeId,
        distanceId: r.distanceId,
        scheduledTime: r.scheduledTime,
        swimmerIds: r.swimmerIds,
      })),
    })),
  };
}

/** DTO tree → id-preserving read model for Finished/Results. Time normalized to 'HH:mm'. */
export function toRaceScheduleList(dto: ScheduleDtoData): RaceScheduleDay[] {
  return dto.days.map((day) => ({
    id: day.id,
    labelEn: day.labelEn,
    labelAr: day.labelAr,
    dayDate: day.dayDate,
    races: day.races.map((r) => ({
      id: r.id,
      strokeId: r.strokeId,
      distanceId: r.distanceId,
      scheduledTime: r.scheduledTime ? r.scheduledTime.slice(0, 5) : null,
      swimmerIds: [...r.swimmerIds],
    })),
  }));
}
