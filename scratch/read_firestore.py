import json, time, hmac, hashlib, base64, urllib.request

with open('firebase-adminsdk.json') as f:
    key_data = json.load(f)

# Use PyCryptodome or cryptography or openssl to sign, but windows powershell or python might have it
# Let's check if cryptography is in pip list or we can use powershell cert/openssl
print("Project ID:", key_data['project_id'])
