package com.game.clubpoker.auth;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.util.concurrent.Executor;
import java.util.concurrent.Executors;

/**
 * Google sign-in for Unity via Android Credential Manager.
 * Shows the "Sign in with Google" sheet listing the phone's Google accounts
 * and returns the chosen account's ID token (audience = our web client ID),
 * which the server verifies. Called from GoogleAuthProvider.cs.
 */
public class GoogleSignInBridge {

    /** Implemented in C# by an AndroidJavaProxy. Called off the Unity thread. */
    public interface Callback {
        void onSuccess(String idToken);
        void onCancel();
        void onError(String message);
    }

    private static final Executor EXECUTOR = Executors.newSingleThreadExecutor();

    public static void signIn(final Activity activity, final String webClientId, final Callback callback) {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                GetSignInWithGoogleOption option =
                        new GetSignInWithGoogleOption.Builder(webClientId).build();

                GetCredentialRequest request = new GetCredentialRequest.Builder()
                        .addCredentialOption(option)
                        .build();

                CredentialManager.create(activity).getCredentialAsync(
                        activity, request, new CancellationSignal(), EXECUTOR,
                        new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                            @Override
                            public void onResult(GetCredentialResponse response) {
                                handleCredential(response.getCredential(), callback);
                            }

                            @Override
                            public void onError(GetCredentialException e) {
                                if (e instanceof GetCredentialCancellationException) {
                                    callback.onCancel();
                                } else {
                                    callback.onError(e.getClass().getSimpleName() + ": " + e.getMessage());
                                }
                            }
                        });
            }
        });
    }

    /** Forget the signed-in account so the next sign-in shows the picker again. */
    public static void signOut(Activity activity) {
        CredentialManager.create(activity).clearCredentialStateAsync(
                new ClearCredentialStateRequest(), new CancellationSignal(), EXECUTOR,
                new CredentialManagerCallback<Void, ClearCredentialException>() {
                    @Override
                    public void onResult(Void result) { }

                    @Override
                    public void onError(ClearCredentialException e) { }
                });
    }

    private static void handleCredential(Credential credential, Callback callback) {
        if (credential instanceof CustomCredential
                && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
            try {
                GoogleIdTokenCredential google =
                        GoogleIdTokenCredential.createFrom(((CustomCredential) credential).getData());
                callback.onSuccess(google.getIdToken());
            } catch (Exception e) {
                callback.onError("Bad Google credential: " + e.getMessage());
            }
        } else {
            callback.onError("Unexpected credential type: " + credential.getType());
        }
    }
}
