import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthStore } from '../../core/auth/auth.store';

/** Signed in, but the account's role does not include the needed permission. */
@Component({
  selector: 'acts-forbidden-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="status-page">
      <h1>Access denied</h1>
      <p>
        The {{ roles() }} account <strong>{{ auth.user()?.userName }}</strong> does not have permission to view
        people records. Sign out and sign in with an account that does, or ask an administrator for access.
      </p>
    </div>
  `,
  styleUrl: './status-page.scss',
})
export class ForbiddenPage {
  protected readonly auth = inject(AuthStore);
  protected readonly roles = () => this.auth.user()?.roles.join(', ') || 'current';
}
