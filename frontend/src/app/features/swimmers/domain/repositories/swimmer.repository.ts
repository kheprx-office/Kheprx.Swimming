import { InjectionToken } from '@angular/core';
import { SwimmerCountItemDtoRs } from '@features/swimmers/data/dto/swimmer-count.dto';
import { CreateSwimmerDtoRq, CreatedSwimmerItemDtoRs } from '@features/swimmers/data/dto/create-swimmer.dto';
import { SwimmerListItemsDtoRs } from '@features/swimmers/data/dto/swimmer-list.dto';

export interface ISwimmerRepository {
  getCount(): Promise<SwimmerCountItemDtoRs>;
  list(search?: string): Promise<SwimmerListItemsDtoRs>;
  create(rq: CreateSwimmerDtoRq): Promise<CreatedSwimmerItemDtoRs>;
}

export const SWIMMER_REPOSITORY = new InjectionToken<ISwimmerRepository>('SWIMMER_REPOSITORY');
