import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PersonStatus } from './person-status';

const LABELS: Record<PersonStatus, string> = {
  'not-assigned': 'Not assigned',
  active: 'Active',
  retired: 'Retired',
};

/** The one place status styling lives. Always text plus colour, never colour alone. */
@Component({
  selector: 'acts-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [class]="'badge badge--' + status()">{{ label() }}</span>`,
  styles: `
    .badge {
      display: inline-flex;
      align-items: center;
      gap: var(--acts-space-1);
      padding: 2px var(--acts-space-3);
      border-radius: 999px;
      border: 1px solid transparent;
      font: var(--mat-sys-label-large);
      white-space: nowrap;
    }

    .badge::before {
      content: '';
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: currentColor;
    }

    .badge--active {
      background: var(--acts-status-active-background);
      color: var(--acts-status-active-text);
    }

    .badge--retired {
      background: var(--acts-status-retired-background);
      color: var(--acts-status-retired-text);
    }

    .badge--not-assigned {
      border-color: var(--acts-status-unassigned-border);
      color: var(--acts-status-unassigned-text);
    }

    .badge--not-assigned::before {
      background: transparent;
      border: 1.5px solid currentColor;
      box-sizing: border-box;
    }
  `,
})
export class StatusBadge {
  readonly status = input.required<PersonStatus>();
  protected readonly label = computed(() => LABELS[this.status()]);
}
