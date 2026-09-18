import { TestBed } from '@angular/core/testing';
import { SkeletonComponent } from './skeleton.component';

describe('SkeletonComponent', () => {
  it('renders a .skeleton element sized by its inputs', () => {
    const fixture = TestBed.createComponent(SkeletonComponent);
    fixture.componentRef.setInput('width', '5rem');
    fixture.componentRef.setInput('height', '2rem');
    fixture.detectChanges();
    const el = fixture.nativeElement.querySelector('.skeleton') as HTMLElement;
    expect(el).toBeTruthy();
    expect(el.style.width).toBe('5rem');
    expect(el.style.height).toBe('2rem');
  });
});
