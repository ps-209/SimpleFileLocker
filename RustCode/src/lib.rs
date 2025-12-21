use std::ffi::CStr;
use std::os::raw::c_char;
use std::fs;
use std::num::NonZeroU32;
use ring::{pbkdf2, aead};
use ring::rand::{SecureRandom, SystemRandom};
use ring::aead::{BoundKey, OpeningKey, SealingKey, UnboundKey};

#[unsafe(no_mangle)]
pub extern "C" fn simple_file(cmode: *const c_char, cfile_path: *const c_char, cpassword: *const c_char, cprotection: *const c_char) -> i32 {
    let (mode, file_path, password, protection) = set_up(cmode, cfile_path, cpassword, cprotection);
    if mode == -1 { return -1; }

    match mode {
        0 => { // 암호화 (Lock)
            if is_locked(&file_path) != "false" { return 4; }
            match fs::read(&file_path) {
                Ok(content) => {
                    match execute_encrypt(content, &password) {
                        Ok(encrypted_data) => {
                            write_file_bytes(&file_path, &protection, encrypted_data);
                            mark_lock(&file_path, if protection == "on" { "protect_on" } else { "protect_off" });
                            0
                        }
                        Err(_) => 1,
                    }
                }
                Err(_) => 2,
            }
        },
        1 => { // 복호화 (Unlock)
            if fs::metadata(&file_path).is_err() {
                return 2; // 파일 읽기 실패 (존재하지 않음)
            }
            let lock_status = is_locked(&file_path);
            if lock_status == "false" { return 4; }
            
            let protect_check = if lock_status == "protect_on" { "on" } else { "off" };
            match read_file_bytes(&file_path, protect_check) {
                Ok(encrypted_data) => {
                    match execute_decrypt(encrypted_data, &password) {
                        Ok(decrypted_data) => {
                            write_file_bytes(&file_path, "off", decrypted_data);
                            mark_lock(&file_path, "false");
                            0
                        }
                        Err(_) => 3,
                    }
                }
                Err(_) => 2,
            }
        },
        _ => -1,
    }
}

// --- 파일 I/O (바이너리 전용) ---

fn read_file_bytes(file_path: &String, protection: &str) -> Result<Vec<u8>, std::io::Error> {
    let target_path = if protection == "on" { format!("{}:secret", file_path) } else { file_path.clone() };
    fs::read(target_path)
}

fn write_file_bytes(file_path: &String, protection: &str, content: Vec<u8>) {
    if protection == "on" {
        let _ = fs::write(format!("{}:secret", file_path), &content);
        let _ = fs::write(file_path, "This file is locked");
    } else {
        let temp_path = format!("{}.tmp", file_path);
        if fs::write(&temp_path, &content).is_ok() {
            let _ = fs::rename(&temp_path, file_path);
        }
    }
}

// --- 암호화 로직 (Base64 제거) ---

fn execute_encrypt(content: Vec<u8>, password: &[u8]) -> Result<Vec<u8>, ring::error::Unspecified> {
    let iterations = NonZeroU32::new(100_000).unwrap();
    let salt = generate_salt()?; 

    let mut auth_data = Vec::with_capacity(salt.len() + 4);
    auth_data.extend_from_slice(&salt);
    auth_data.extend_from_slice(&iterations.get().to_le_bytes());

    let derived_key_bytes = derive_key(password, &salt, iterations)?;
    let mut sealing_key = gen_sealing_key(derived_key_bytes, CounterNonce(1))?;
    
    let mut in_out = content;
    let tag = sealing_key.seal_in_place_separate_tag(aead::Aad::from(auth_data), &mut in_out)?;

    // 바이너리 데이터 결합: [Salt][Ciphertext][Tag]
    let mut result = Vec::with_capacity(salt.len() + in_out.len() + tag.as_ref().len());
    result.extend_from_slice(&salt);
    result.extend_from_slice(&in_out);
    result.extend_from_slice(tag.as_ref());
    
    Ok(result)
}

fn execute_decrypt(encrypted_data: Vec<u8>, password: &[u8]) -> Result<Vec<u8>, ring::error::Unspecified> {
    const SALT_LEN: usize = 16;
    const TAG_LEN: usize = 16;

    if encrypted_data.len() < SALT_LEN + TAG_LEN {
        return Err(ring::error::Unspecified);
    }

    // 데이터 분해
    let salt = encrypted_data[..SALT_LEN].to_vec();
    let ciphertext_with_tag = encrypted_data[SALT_LEN..].to_vec(); // [Ciphertext][Tag] 형태

    let iterations = NonZeroU32::new(100_000).unwrap();
    let mut auth_data = Vec::with_capacity(SALT_LEN + 4);
    auth_data.extend_from_slice(&salt);
    auth_data.extend_from_slice(&iterations.get().to_le_bytes());

    let derived_key_bytes = derive_key(password, &salt, iterations)?;
    let opening_key = gen_opening_key(derived_key_bytes, CounterNonce(1))?;
    
    // open_in_place는 끝에 붙은 태그를 자동으로 검증합니다.
    let in_out = ciphertext_with_tag;
    let decrypted_bytes = decrypt(in_out, auth_data, opening_key)?; 
    
    Ok(decrypted_bytes)
}

// --- 공통 유틸리티 ---

fn set_up(a: *const c_char, b: *const c_char, c: *const c_char, d: *const c_char) -> (i32, String, Vec<u8>, String) {
    if a.is_null() || b.is_null() || c.is_null() || d.is_null() {
        return (-1, String::new(), Vec::new(), String::new());
    }
    unsafe {
        // 모드 변환
        let mode_str = CStr::from_ptr(a).to_string_lossy().to_lowercase();
        let mode = if mode_str == "lock" { 0 } else if mode_str == "unlock" { 1 } else { -1 };
        
        // 경로 변환 (C#에서 UTF-8로 보낸 데이터를 읽음)
        let path = CStr::from_ptr(b).to_str().unwrap_or("").to_string();
        
        // 비밀번호 변환
        let pass_slice = CStr::from_ptr(c).to_str().unwrap_or("");
        let password = if pass_slice.is_empty() { b"20251201".to_vec() } else { pass_slice.as_bytes().to_vec() };
        
        // 보호 모드 변환
        let protection = CStr::from_ptr(d).to_str().unwrap_or("").to_string();
        
        (mode, path, password, protection)
    }
}

fn is_locked(file_path: &String) -> String {
    let check_path = format!("{}:locked", file_path);
    fs::read_to_string(check_path).unwrap_or_else(|_| "false".to_string())
}

fn mark_lock(file_path: &String, condition: &str) {
    let mark_path = format!("{}:locked", file_path);
    let _ = fs::write(mark_path, condition);
}

struct CounterNonce(u32);
impl aead::NonceSequence for CounterNonce {
    fn advance(&mut self) -> Result<aead::Nonce, ring::error::Unspecified> {
        let mut nonce = vec![0; aead::NONCE_LEN];
        nonce[8..].copy_from_slice(&self.0.to_be_bytes());
        self.0 += 1;
        aead::Nonce::try_assume_unique_for_key(&nonce)
    }
}

fn generate_salt() -> Result<Vec<u8>, ring::error::Unspecified> {
    let mut salt = vec![0; 16];
    SystemRandom::new().fill(&mut salt)?;
    Ok(salt)
}

fn derive_key(pw: &[u8], salt: &[u8], iter: NonZeroU32) -> Result<Vec<u8>, ring::error::Unspecified> {
    let mut key = vec![0; 32];
    pbkdf2::derive(pbkdf2::PBKDF2_HMAC_SHA256, iter, salt, pw, &mut key);
    Ok(key)
}

fn gen_sealing_key(derived_bytes: Vec<u8>, nonce: CounterNonce) -> Result<SealingKey<CounterNonce>, ring::error::Unspecified> {
    let ub_key = UnboundKey::new(&aead::AES_256_GCM, &derived_bytes)?;
    // SealingKey::new는 Result가 아닌 객체를 반환하므로 Ok()로 감싸줍니다.
    Ok(SealingKey::new(ub_key, nonce))
}

/// 복호화 키를 생성
fn gen_opening_key(derived_bytes: Vec<u8>, nonce: CounterNonce) -> Result<OpeningKey<CounterNonce>, ring::error::Unspecified> {
    let ub_key = UnboundKey::new(&aead::AES_256_GCM, &derived_bytes)?;
    // OpeningKey::new 역시 Ok()로 감싸줍니다.
    Ok(OpeningKey::new(ub_key, nonce))
}

fn decrypt(mut data: Vec<u8>, auth: Vec<u8>, mut key: OpeningKey<CounterNonce>) -> Result<Vec<u8>, ring::error::Unspecified> {
    let decrypted = key.open_in_place(aead::Aad::from(auth), &mut data)?;
    Ok(decrypted.to_vec())
}

#[cfg(test)]
mod tests {
    //use super::*;

    #[test]
    fn it_works() {
        println!("hi");
    }
}
