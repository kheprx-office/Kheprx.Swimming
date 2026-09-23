import { CompetitionEventDtoRs } from '@features/championships/data/dto/competition-event.dto';
import { Championship } from '@features/championships/domain/model/championship';

export function toChampionship(d: CompetitionEventDtoRs): Championship {
  return {
    id: d.id,
    nameEn: d.nameEn,
    nameAr: d.nameAr,
    startDate: d.startDate,
    endDate: d.endDate,
    locationEn: d.locationEn,
    locationAr: d.locationAr,
    statusId: d.statusId,
    statusCode: d.statusCode,
    statusNameEn: d.statusNameEn,
    statusNameAr: d.statusNameAr,
  };
}

export function toChampionshipList(list: CompetitionEventDtoRs[]): Championship[] {
  return list.map(toChampionship);
}
