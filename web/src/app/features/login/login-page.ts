import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { form, FormField, FormRoot, required, TreeValidationResult } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthStore, LoginOutcome } from '../../core/auth/auth.store';
import { safeReturnUrl } from '../../core/navigation/return-url';

const FAILURE_MESSAGES: Record<Exclude<LoginOutcome, 'signed-in'>, string> = {
  'invalid-credentials': 'Incorrect username or password.',
  'csrf-failed': 'Your sign-in page expired. Please sign in again.',
  unavailable: 'The ACTS service is unavailable. Try again shortly.',
  failed: 'Sign-in failed. Try again.',
};

@Component({
  selector: 'acts-login-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormRoot, FormField, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule, RouterLink],
  templateUrl: './login-page.html',
  styleUrl: './login-page.scss',
})
export class LoginPage {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly showPassword = signal(false);

  private readonly credentials = signal({ userName: '', password: '' });

  protected readonly loginForm = form(
    this.credentials,
    (path) => {
      required(path.userName, { message: 'Enter your username.' });
      required(path.password, { message: 'Enter your password.' });
    },
    { submission: { action: () => this.signIn() } },
  );

  private async signIn(): Promise<TreeValidationResult> {
    const outcome = await this.auth.login(this.credentials());
    if (outcome === 'signed-in') {
      this.credentials.update((value) => ({ ...value, password: '' }));
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
      await this.router.navigateByUrl(safeReturnUrl(returnUrl));
      return undefined;
    }
    return { kind: outcome, message: FAILURE_MESSAGES[outcome] };
  }
}
