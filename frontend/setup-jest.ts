// setup-jest.ts — initialise the Angular TestBed zone environment for Jest.
// @angular/compiler must be imported first so partially-compiled Angular libraries
// (e.g. @lucide/angular) can use the JIT fallback path in test environments.
import '@angular/compiler';
import { setupZoneTestEnv } from 'jest-preset-angular/setup-env/zone';

setupZoneTestEnv();
